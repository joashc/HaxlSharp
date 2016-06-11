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
    public static partial class Base
    {
        public static IEnumerable<A> Append<A>(this IEnumerable<A> list, A value)
        {
            var appendList = new List<A>(list);
            appendList.Add(value);
            return appendList;
        }

        public static ShowList<A> ShowList<A>(IEnumerable<A> list)
        {
            return new ShowList<A>(list);
        }

        public static InformationLogEntry Info(string info)
        {
            return new InformationLogEntry(info);
        }

        public static WarningLogEntry Warn(string warn)
        {
            return new WarningLogEntry(warn);
        }

        public static ErrorLogEntry Error(string error)
        {
            return new ErrorLogEntry(error);
        }

        public static Unit UnitVal = new Unit();
    }
}
