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
        IEnumerable<BindExpression> Binds { get; }
    }

    public class BindExpr<A, B, C> : Expr<C>
    {
        public BindExpr(IEnumerable<BindExpression> binds, Expr<A> expr)
        {
            _binds = binds;
            Expr = expr;
        }

        private readonly IEnumerable<BindExpression> _binds;
        public IEnumerable<BindExpression> Binds { get { return _binds; } }

        public readonly Expr<A> Expr;
    }

    public class BindExpression
    {
        public BindExpression(LambdaExpression bind, LambdaExpression project)
        {
            Bind = bind;
            Project = project;
        }

        public readonly LambdaExpression Bind;
        public readonly LambdaExpression Project;
    }

    public class ApplicativeGroup
    {
        public ApplicativeGroup(List<BindExpression> expressions = null, List<string> boundVariables = null)
        {
            Expressions = expressions ?? new List<BindExpression>();
            BoundVariables = boundVariables ?? new List<string>();
        }

        public readonly List<BindExpression> Expressions;
        public readonly List<string> BoundVariables;
    }

    public class Identity<A> : Expr<A>
    {
        public readonly A val;
        public Identity(A val)
        {
            this.val = val;
        }

        private static readonly IEnumerable<BindExpression> emptyList = new List<BindExpression>();
        public IEnumerable<BindExpression> Binds { get { return emptyList; } }
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
            var bindExpression = new BindExpression(bind, project);
            var newBinds = self.Binds.Append(bindExpression);
            return new BindExpr<A, B, C>(newBinds, self);
        }
    }

}
