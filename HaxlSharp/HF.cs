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
        public ApplicativeGroup(bool isProjectGroup = false, List<LambdaExpression> expressions = null, List<string> boundVariables = null)
        {
            Expressions = expressions ?? new List<LambdaExpression>();
            BoundVariables = boundVariables ?? new List<string>();
            IsProjectGroup = isProjectGroup;
        }

        public readonly List<LambdaExpression> Expressions;
        public readonly List<string> BoundVariables;
        public readonly bool IsProjectGroup;
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
            Expression<Func<A, Expr<B>>> bind = a => new Identity<B>(f(a));
            Expression<Func<A, B, B>> project = (a, b) => b;
            var newBinds = new BindProjectPair(bind, project);
            return new BindExpr<A, B, B>(self.CollectedExpressions.Append(newBinds), self);
        }

        public static Expr<C> SelectMany<A, B, C>(this Expr<A> self, Expression<Func<A, Expr<B>>> bind, Expression<Func<A, B, C>> project)
        {
            var bindExpression = new BindProjectPair(bind, project);
            var newBinds = self.CollectedExpressions.Append(bindExpression);
            return new BindExpr<A, B, C>(newBinds, self);
        }

        public static Expr<IEnumerable<A>> Sequence<A>(this IEnumerable<Expr<A>> dists)
        {
            var results = dists.Select(d => RunSplits.Run(Splitter.Split(d))).ToArray();
            Task.WaitAll(results);
            return new Identity<IEnumerable<A>>(results.Select(task => task.Result));
        }

        public static Task<A> Fetch<A>(this Expr<A> expr)
        {
            var split = Splitter.Split(expr);
            return RunSplits.Run(split);
        }



   } 

}
