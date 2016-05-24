using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    /// <summary>
    /// Contains constructor functions.
    /// </summary>
    public static class Haxl
    {
        public static IEnumerable<A> Append<A>(IEnumerable<A> list, A value)
        {
            var appendList = new List<A>(list);
            appendList.Add(value);
            return appendList;
        }

        public static Result<A> Done<A>(Func<A> result)
        {
            return new Done<A>(result);
        }

        public static Result<A> Blocked<A>(Result<A> fetch, IEnumerable<Task> blockedRequests)
        {
            return new Blocked<A>(fetch, blockedRequests);
        }

    }
}
