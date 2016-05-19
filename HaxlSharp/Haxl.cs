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

        public static Fetch<A> Done<A>(A result)
        {
            return new Done<A>(result);
        }

        public static Fetch<A> Blocked<A>(Task<Fetch<A>> fetch, IEnumerable<Task> blockedRequests)
        {
            return new Blocked<A>(fetch, blockedRequests);
        }

        public static Fetch<C> Applicative<A, B, C>(Fetch<A> f1, Fetch<B> f2, Func<A, B, C> project)
        {
            if (f1 is Done<A> && f2 is Done<A>)
            {
                var r1 = f1.Fetch();
                var r2 = f2.Fetch();
                await Task.WhenAll(r1, r2);
                return Done(project(r1.Result, r2.Result));
            }
            if (f1 is Done<A> && f2 is Blocked<B>)
            {
                var blocked2 = f2 as Blocked<B>;
                var fetchB = await blocked2.fetch;
                var newFetch = Applicative(f1, fetchB, project);
                return Blocked(newFetch, blocked2.blockedRequests);
            }
            if (f1 is Blocked<A> && f2 is Done<B>)
            {
                var blocked1 = f1 as Blocked<A>;
                var fetchA = await blocked1.fetch;
                var newFetch = Applicative(fetchA, f2, project);
                return Blocked(newFetch, blocked1.blockedRequests);
            }
            if (f1 is Blocked<A> && f2 is Blocked<B>)
            {
                var blockedA = f1 as Blocked<A>;
                var blockedB = f2 as Blocked<B>;

                var fetchA = await blockedA.fetch;
                var fetchB = await blockedB.fetch;
                var newFetch = Applicative(fetchA, fetchB, project);

                return Blocked(newFetch, blockedA.blockedRequests.Concat(blockedB.blockedRequests));
            }
            throw new ArgumentException();
        }
    }
}
