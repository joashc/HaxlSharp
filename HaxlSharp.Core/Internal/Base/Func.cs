using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp.Internal
{
    public static partial class Base
    {
        public static Func<A> func<A>(Func<A> func) { return func; }
        public static Func<A,B> func<A,B>(Func<A,B> func) { return func; }
        public static Func<A,B,C> func<A,B,C>(Func<A,B,C> func) { return func; }
        public static Func<A,B,C,D> func<A,B,C,D>(Func<A,B,C,D> func) { return func; }

        public static Func<A, C> compose<A, B, C>(Func<B, C> f2, Func<A, B> f1) { return x => f2(f1(x)); }
        public static Func<A, D> compose<A, B, C, D>(Func<C, D> f3, Func<B, C> f2, Func<A, B> f1) { return x => f3(f2(f1(x))); }
        public static Func<A, E> compose<A, B, C, D, E>(Func<D, E> f4, Func<C, D> f3, Func<B, C> f2, Func<A, B> f1) { return x => f4(f3(f2(f1(x)))); }
        public static Func<A, F> compose<A, B, C, D, E, F>(Func<E, F> f5, Func<D, E> f4, Func<C, D> f3, Func<B, C> f2, Func<A, B> f1) { return x => f5(f4(f3(f2(f1(x))))); }


    }
}
