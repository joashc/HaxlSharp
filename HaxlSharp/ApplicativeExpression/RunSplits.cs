using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface ApplicExpr<A>
    {
    }

    public class ABind<A, B, C> : ApplicExpr<C>
    {
        public readonly Dictionary<string, object> boundVariables;
        public readonly ApplicativeGroup Before;
        public readonly ApplicativeGroup After;
    }


    public static class RunSplits
    {
        public static A FetchId<A>(Identity<A> id)
        {
            return id.val;
        }

        public static A Run<A>(SplitApplicatives<A> splits)
        {
            throw new ArgumentException();
        }
    }
}
