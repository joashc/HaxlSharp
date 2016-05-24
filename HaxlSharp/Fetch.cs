using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static HaxlSharp.Haxl;

namespace HaxlSharp
{
    public interface FetchMonad<A>
    {
        X Run<X>(FetchRewriter<A, X> rewriter);
    }

    public interface FetchRewriter<C, X>
    {
        X Bind<B>(FetchMonad<B> fetch, Expression<Func<B, FetchMonad<C>>> bind);
        X Applicative<A, B>(FetchMonad<A> fetch1, Func<FetchMonad<B>> fetch2, Func<A, B, C> project);
        X Result(Result<C> result);
    }

    public class Fetch<A>
    {
        public Result<A> Result { get; }
        public Fetch(Result<A> result)
        {
            Result = result;
        }
    }

    public interface Result<A> : FetchMonad<A>
    {
        X Run<X>(Fetcher<A, X> fetcher);
    }

    public class Done<A> : Result<A>
    {
        public readonly Func<A> result;
        public Done(Func<A> result)
        {
            this.result = result;
        }

        public X Run<X>(FetchRewriter<A, X> rewriter)
        {
            return rewriter.Result(this);
        }

        public X Run<X>(Fetcher<A, X> fetcher)
        {
            return fetcher.Done(result);
        }
    }

    public class Bind<B, C> : FetchMonad<C>
    {
        public readonly FetchMonad<B> fetch;
        public readonly Expression<Func<B, FetchMonad<C>>> bind;
        public Bind(FetchMonad<B> fetch, Expression<Func<B, FetchMonad<C>>> bind)
        {
            this.fetch = fetch;
            this.bind = bind;
        }

        public X Run<X>(FetchRewriter<C, X> rewriter)
        {
            return rewriter.Bind(fetch, bind);
        }
    }

    public class Applicative<A, B, C> : FetchMonad<C>
    {
        public readonly FetchMonad<A> fetch1;
        public readonly Func<FetchMonad<B>> fetch2;
        public readonly Func<A, B, C> project;
        public Applicative(FetchMonad<A> fetch1, Func<FetchMonad<B>> fetch2, Func<A, B, C> project)
        {
            this.fetch1 = fetch1;
            this.fetch2 = fetch2;
            this.project = project;
        }

        public X Run<X>(FetchRewriter<C, X> rewriter)
        {
            return rewriter.Applicative(fetch1, fetch2, project);
        }
    }

    public class Blocked<A> : Result<A>
    {
        public readonly Fetch<A> fetch;
        public readonly IEnumerable<Task> blockedRequests;
        public Blocked(Fetch<A> fetch, IEnumerable<Task> blockedRequests)
        {
            this.fetch = fetch;
            this.blockedRequests = blockedRequests;
        }

        public X Run<X>(Fetcher<A, X> fetcher)
        {
            return fetcher.Blocked(fetch, blockedRequests);
        }

        public X Run<X>(FetchRewriter<A, X> rewriter)
        {
            return rewriter.Result(this);
        }
    }

    public static class FetchMonadExt
    {
        public static FetchMonad<B> Select<A, B>(this FetchMonad<A> self, Expression<Func<A, B>> f)
        {
            var compiled = f.Compile();
            return Done(() => compiled(self.Rewrite().RunFetch().Result));
        }

        public static FetchMonad<B> SelectMany<A, B>(this FetchMonad<A> self, Expression<Func<A, FetchMonad<B>>> bind)
        {
            return new Bind<A, B>(self, bind);
        }

        public static FetchMonad<C> SelectMany<A, B, C>(this FetchMonad<A> self,
            Expression<Func<A, FetchMonad<B>>> bind, Expression<Func<A, B, C>> project)
        {
            var compiledBind = bind.Compile();
            var compiledProject = project.Compile();

            if (DetectApplicative.IsApplicative(bind)) return new Applicative<A, B, C>(self, () => compiledBind(default(A)), compiledProject);

            return new Bind<A, C>(self, a => new Bind<B, C>(compiledBind(a),
                b => Done(() => compiledProject(a, b))));
        }
    }

    public class Rewriter<C> : FetchRewriter<C, Fetch<C>>
    {
        public Fetch<C> Applicative<A, B>(FetchMonad<A> fetch1, Func<FetchMonad<B>> fetch2, Func<A, B, C> project)
        {
            var fetchA = fetch1.Rewrite();
            var fetchB = fetch2().Rewrite();
            return Haxl.Applicative(fetchA, fetchB, project);
        }

        public Fetch<C> Bind<B>(FetchMonad<B> fetch, Expression<Func<B, FetchMonad<C>>> bind)
        {
            var compiledBind = bind.Compile();

            var fetchB = fetch.Rewrite();
            var fetchC = Done(() => compiledBind(fetchB.RunFetch().Result).Rewrite().RunFetch().Result);
            return Fetch(fetchC);
        }

        public Fetch<C> Result(Result<C> result)
        {
            return Fetch(result);
        }
    }

    public static class FetchExt
    {
        public static Fetch<B> Select<A, B>(this Fetch<A> fetch, Func<A, B> f)
        {
            var resultA = fetch.Result;
            if (resultA is Done<A>)
            {
                var doneA = resultA as Done<A>;
                return Fetch(Done(() => f(doneA.result())));
            }
            if (resultA is Blocked<A>)
            {
                var blockedA = resultA as Blocked<A>;
                return Fetch(Blocked(blockedA.fetch.Select(f), blockedA.blockedRequests));
            }
            throw new ArgumentException();
        }

        public static Fetch<B> SelectMany<A, B>(this Fetch<A> fetch, Func<A, Fetch<B>> bind)
        {
            var resultA = fetch.Result;
            if (resultA is Done<A>)
            {
                var doneA = resultA as Done<A>;
                return bind(doneA.result());
            }
            if (resultA is Blocked<A>)
            {
                var blockedA = resultA as Blocked<A>;
                var newFetch = blockedA.fetch.SelectMany(bind);
                return Fetch(Blocked(newFetch, blockedA.blockedRequests));
            }
            throw new ArgumentException();
        }

        public static Fetch<C> SelectMany<A, B, C>(this Fetch<A> fetch, Func<A, Fetch<B>> bind, Func<A, B, C> project)
        {
            return fetch.SelectMany(t => bind(t).Select(u => project(t, u)));
        }

        public static Task<A> RunFetch<A>(this Fetch<A> fetch)
        {
            return fetch.Result.Run(new RunFetch<A>());
        }

        public static Fetch<A> Rewrite<A>(this FetchMonad<A> fetch)
        {
            return fetch.Run(new Rewriter<A>());
        }

        /// <summary>
        /// Default to using recursion depth limit of 100
        /// </summary>
        public static FetchMonad<IEnumerable<A>> Sequence<A>(this IEnumerable<FetchMonad<A>> dists)
        {
            return SequenceWithDepth(dists, 10);
        }

        /// <summary>
        /// This implementation sort of does trampolining to avoid stack overflows,
        /// but for performance reasons it recursively divides the list
        /// into groups up to a recursion depth, instead of trampolining every iteration.
        ///
        /// This should limit the recursion depth to around 
        /// $$s\log_{s}{n}$$
        /// where s is the specified recursion depth limit
        /// </summary>
        public static FetchMonad<IEnumerable<A>> SequenceWithDepth<A>(this IEnumerable<FetchMonad<A>> dists, int recursionDepth)
        {
            var sections = dists.Count() / recursionDepth;
            if (sections <= 1) return RunSequence(dists);
            return from nested in SequenceWithDepth(SequencePartial(dists, recursionDepth), recursionDepth)
                   select nested.SelectMany(a => a);
        }

        /// <summary>
        /// `sequence` can be implemented as
        /// sequence xs = foldr (liftM2 (:)) (return []) xs
        /// </summary>
        private static FetchMonad<IEnumerable<A>> RunSequence<A>(IEnumerable<FetchMonad<A>> dists)
        {
            return dists.Aggregate(
                Done<IEnumerable<A>>(() => new List<A>()) as FetchMonad<IEnumerable<A>>,
                (listFetch, aFetch) => from a in aFetch
                                       from list in listFetch
                                       select Append(list, a)
            );
        }

        /// <summary>
        /// Divide a list of distributions into groups of given size, then runs sequence on each group
        /// </summary>
        /// <returns>The list of sequenced distribution groups</returns>
        private static IEnumerable<FetchMonad<IEnumerable<A>>> SequencePartial<A>(IEnumerable<FetchMonad<A>> dists, int groupSize)
        {
            var numGroups = dists.Count() / groupSize;
            return Enumerable.Range(0, numGroups)
                             .Select(groupNum => RunSequence(dists.Skip(groupNum * groupSize).Take(groupSize)));
        }

    }

}
