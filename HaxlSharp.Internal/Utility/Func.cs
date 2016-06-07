using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp.Internal
{
    public static partial class Haxl
    {
        public static Func<A> func<A>(Func<A> func) { return func; }
        public static Func<A,B> func<A,B>(Func<A,B> func) { return func; }
        public static Func<A,B,C> func<A,B,C>(Func<A,B,C> func) { return func; }
        public static Func<A,B,C,D> func<A,B,C,D>(Func<A,B,C,D> func) { return func; }

        public static Func<A, C> comp<A, B, C>(Func<A, B> f1, Func<B, C> f2)
        {
            return a => f2(f1(a));
        }

    }
}
