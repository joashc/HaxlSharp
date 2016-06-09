﻿using System;
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

        public static bool Not(bool b)
        {
            return !b;
        }

        public static Unit Unit = new Unit();
    }
}
