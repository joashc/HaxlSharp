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
        public static IEnumerable<A> Append<A>(this IEnumerable<A> list, A value)
        {
            var appendList = new List<A>(list);
            appendList.Add(value);
            return appendList;
        }

        public static Fetch<A> ToFetch<A>(this Returns<A> request)
        {
            return new Request<A>(request);
        }
    }
}
