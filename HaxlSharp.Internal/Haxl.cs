using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// Contains constructor functions.
    /// </summary>
    public static partial class Haxl
    {
        public static IEnumerable<A> Append<A>(this IEnumerable<A> list, A value)
        {
            var appendList = new List<A>(list);
            appendList.Add(value);
            return appendList;
        }

        public static string PrefixedVariable(int blockNumber, string variableName)
        {
            return $"({blockNumber}) {variableName}";
        }
    }
}
