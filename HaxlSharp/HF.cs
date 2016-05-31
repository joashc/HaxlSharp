using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface Expr<A>
    {
        IEnumerable<BindProjectPair> CollectedExpressions { get; }
        LambdaExpression Initial { get; }
    }



    public class BindExpr<A, B, C> : Expr<C>
    {
        public BindExpr(IEnumerable<BindProjectPair> binds, Expr<A> expr)
        {
            _binds = binds;
            Expr = expr;
        }

        private readonly IEnumerable<BindProjectPair> _binds;
        public IEnumerable<BindProjectPair> CollectedExpressions { get { return _binds; } }

        public LambdaExpression Initial
        {
            get { return Expr.Initial; }
        }

        public readonly Expr<A> Expr;
    }

    public class BindProjectPair
    {
        public BindProjectPair(LambdaExpression bind, LambdaExpression project)
        {
            Bind = bind;
            Project = project;
        }

        public readonly LambdaExpression Bind;
        public readonly LambdaExpression Project;
    }

    public class ApplicativeGroup
    {
        public ApplicativeGroup(List<LambdaExpression> expressions = null, List<string> boundVariables = null)
        {
            Expressions = expressions ?? new List<LambdaExpression>();
            BoundVariables = boundVariables ?? new List<string>();
        }

        public readonly List<LambdaExpression> Expressions;
        public readonly List<string> BoundVariables;
    }

    public class Identity<A> : Expr<A>
    {
        public readonly A val;
        public Identity(A val)
        {
            this.val = val;
        }

        private static readonly IEnumerable<BindProjectPair> emptyList = new List<BindProjectPair>();
        public IEnumerable<BindProjectPair> CollectedExpressions { get { return emptyList; } }

        public LambdaExpression Initial { get { return Expression.Lambda(Expression.Constant(this)); } }
    }

    public static class ExprExt
    {
        public static Expr<B> Select<A, B>(this Expr<A> self, Func<A, B> f)
        {
            return from a in self
                   select f(a);
        }

        public static Expr<C> SelectMany<A, B, C>(this Expr<A> self, Expression<Func<A, Expr<B>>> bind, Expression<Func<A, B, C>> project)
        {
            var bindExpression = new BindProjectPair(bind, project);
            var newBinds = self.CollectedExpressions.Append(bindExpression);
            return new BindExpr<A, B, C>(newBinds, self);
        }
        /// <summary>
        /// Default to using recursion depth limit of 100
        /// </summary>
        public static Expr<IEnumerable<A>> Sequence<A>(this IEnumerable<Expr<A>> dists)
        {
            var results = dists.Select(d => RunSplits.Run(Splitter.Split(d))).ToArray();
            Task.WaitAll(results);
            return new Identity<IEnumerable<A>>(results.Select(task => task.Result));
        }

        /// <summary>
        /// This implementation sort of does trampolining to avoid stack overflows,
        /// but for performance reasons it recursively divides the list
        /// into groups up to a recursion depth, instead of trampolining every iteration.
        ///
        /// This should limit the recursion depth to around 
        /// $$s\log_{s}{n}$$
        /// where s is the specified recursion depth limit
        /// </summary>
        public static Expr<IEnumerable<A>> SequenceWithDepth<A>(this IEnumerable<Expr<A>> dists, int recursionDepth)
        {
            var sections = dists.Count() / recursionDepth;
            if (sections <= 1) return RunSequence(dists);
            return from nested in SequenceWithDepth(SequencePartial(dists, recursionDepth), recursionDepth)
                   select nested.SelectMany(a => a);
        }

        /// <summary>
        /// `sequence` can be implemented as
        /// sequence xs = foldr (liftM2 (:)) (return []) xs
        /// </summary>
        private static Expr<IEnumerable<A>> RunSequence<A>(IEnumerable<Expr<A>> dists)
        {
            return dists.Aggregate(
                new Identity<IEnumerable<A>>(new List<A>()) as Expr<IEnumerable<A>>,
                (listFetch, aFetch) => from a in aFetch
                                       from list in listFetch
                                       select list.Append(a)
            );
        }

        /// <summary>
        /// Divide a list of distributions into groups of given size, then runs sequence on each group
        /// </summary>
        /// <returns>The list of sequenced distribution groups</returns>
        private static IEnumerable<Expr<IEnumerable<A>>> SequencePartial<A>(IEnumerable<Expr<A>> dists, int groupSize)
        {
            var numGroups = dists.Count() / groupSize;
            return Enumerable.Range(0, numGroups)
                             .Select(groupNum => RunSequence(dists.Skip(groupNum * groupSize).Take(groupSize)));
        }


   } 

}
