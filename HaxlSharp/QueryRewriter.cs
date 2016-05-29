using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static HaxlSharp.Haxl;

namespace HaxlSharp
{
    /// <summary>
    /// Traverses a lambda expression and rewrites it in applicative form if possible.
    /// </summary>
    public interface FetchRewriter<C, X>
    {
        X Bind<B>(Fetch<B> fetch, Expression<Func<B, Fetch<C>>> bind, Dictionary<string, object> previouslyBound);
        X Applicative<A, B>(Fetch<A> fetch1, Func<Fetch<B>> fetch2, Func<A, B, C> project);
        X Result(Result<C> result);
    }

    public class Rewriter<C> : FetchRewriter<C, Result<C>>
    {
        /// <summary>
        /// Traverses both branches of the applicative, batching as much as possible.
        /// </summary>
        private static Result<C> Applicative<A, B>(Result<A> resultA, Result<B> resultB, Func<A, B, C> project)
        {
            if (resultA is Done<A> && resultB is Done<B>)
            {
                var doneA = resultA as Done<A>;
                var doneB = resultB as Done<B>;
                return Done(() => project(doneA.result(), doneB.result()));
            }
            if (resultA is Done<A> && resultB is Blocked<B>)
            {
                var doneA = resultA as Done<A>;
                var blockedB = resultB as Blocked<B>;
                var newFetch = Applicative(doneA, blockedB.result, project);
                return Blocked(newFetch, blockedB.blockedRequests);
            }
            if (resultA is Blocked<A> && resultB is Done<B>)
            {
                var blockedA = resultA as Blocked<A>;
                var doneB = resultB as Done<B>;
                var fetchA = blockedA.result;
                var newFetch = Applicative(fetchA, doneB, project);
                return Blocked(newFetch, blockedA.blockedRequests);
            }
            if (resultA is Blocked<A> && resultB is Blocked<B>)
            {
                var blockedA = resultA as Blocked<A>;
                var blockedB = resultB as Blocked<B>;

                var fetchA = blockedA.result;
                var fetchB = blockedB.result;
                var newFetch = Applicative(fetchA, fetchB, project);

                return Blocked(newFetch, blockedB.blockedRequests.Concat(blockedA.blockedRequests));
            }
            throw new ArgumentException();
        }

        /// <summary>
        /// Traverses both branches of the applicative, batching as much as possible.
        /// </summary>
        public Result<C> Applicative<A, B>(Fetch<A> fetch1, Func<Fetch<B>> fetch2, Func<A, B, C> project)
        {
            var fetchA = fetch1.Rewrite();
            var fetchB = fetch2().Rewrite();
            return Applicative(fetchA, fetchB, project);
        }

        /// <summary>
        /// Sequential evaluation of monad. 
        /// </summary>
        public Result<C> Bind<B>(Fetch<B> fetch, Expression<Func<B, Fetch<C>>> bind, Dictionary<string, object> previouslyBound)
        {
            var compiledBind = bind.Compile();
            var fetchB = fetch.Rewrite();
            var applicativeInfo = DetectApplicative.CheckApplicative(bind, previouslyBound);
            return Done(() => compiledBind(fetchB.RunFetch().Result).Rewrite().RunFetch().Result);
        }

        /// <summary>
        /// Pure/ return
        /// </summary>
        public Result<C> Result(Result<C> result)
        {
            return result;
        }
    }
}
