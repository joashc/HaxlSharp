using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp.Utility
{
    public static partial class Haxl
    {
        public static Func<A> Func<A>(Func<A> func) { return func; }
        public static Func<A,B> Func<A,B>(Func<A,B> func) { return func; }
        public static Func<A,B,C> Func<A,B,C>(Func<A,B,C> func) { return func; }
        public static Func<A,B,C,D> Func<A,B,C,D>(Func<A,B,C,D> func) { return func; }
    }
}
