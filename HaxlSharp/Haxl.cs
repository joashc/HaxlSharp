using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public static class Haxl
    {
        public static IEnumerable<A> Append<A>(IEnumerable<A> list, A value)
        {
            var appendList = new List<A>(list);
            appendList.Add(value);
            return appendList;
        }

        public static Result<A> Done<A>(A result)
        {
            return new Done<A>(result);
        }

        public static Result<A> Blocked<A>(Fetch<A> fetch, IEnumerable<Task> blockedRequests)
        {
            return new Blocked<A>(fetch, blockedRequests);
        }

        public static Fetch<A> Fetch<A>(Task<Result<A>> fetchTask)
        {
            return new Fetch<A>(fetchTask);
        }

        public static Fetch<A> Fetch<A>(Result<A> result)
        {
            return new Fetch<A>(Task.FromResult(result));
        }

        public static Fetch<C> Applicative<A, B, C>(Fetch<A> f1, Fetch<B> f2, Func<A, B, C> project)
        {
            var task = Task.Run(async () =>
            {
                var resultA = await f1.Result;
                var resultB = await f2.Result;
                if (resultA is Done<A> && resultB is Done<B>)
                {
                    var doneA = resultA as Done<A>;
                    var doneB = resultB as Done<B>;
                    return Done(project(doneA.result, doneB.result));
                }
                if (resultA is Done<A> && resultB is Blocked<B>)
                {
                    var doneA = resultA as Done<A>;
                    var blockedB = resultB as Blocked<B>;
                    var newFetch = Applicative(Fetch(doneA), blockedB.fetch, project);
                    return Blocked(newFetch, blockedB.blockedRequests);
                }
                if (resultA is Blocked<A> && resultB is Done<B>)
                {
                    var blockedA = resultA as Blocked<A>;
                    var doneB = resultB as Done<B>;
                    var fetchA = blockedA.fetch;
                    var newFetch = Applicative(fetchA, Fetch(doneB), project);
                    return Blocked(newFetch, blockedA.blockedRequests);
                }
                if (resultA is Blocked<A> && resultB is Blocked<B>)
                {
                    var blockedA = resultA as Blocked<A>;
                    var blockedB = resultB as Blocked<B>;

                    var fetchA = blockedA.fetch;
                    var fetchB = blockedB.fetch;
                    var newFetch = Applicative(fetchA, fetchB, project);

                    return Blocked(newFetch, blockedA.blockedRequests.Concat(blockedB.blockedRequests));
                }
                throw new ArgumentException();
            });
            return new Fetch<C>(task);
        }
    }
}
