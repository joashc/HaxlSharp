using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static HaxlSharp.Internal.Base;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// Replaces all transparent identifier or parameter accessor expressions with expressions that read from scope parameter.
    /// </summary>
    public class RebindToScope : ExpressionVisitor
    {
        private List<string> paramNames;
        private ParameterExpression scopeParam;
        public int BlockCount { get; set; }
        private MethodInfo GetValue = typeof(Scope).GetMethod("GetValue");

        /// <summary>
        /// Replace all parameters with a single scope parameter, then rewrite body to read from that scope.
        /// </summary>
        public LambdaExpression Rebind(LambdaExpression lambda)
        {
            paramNames = lambda.Parameters.Select(p => p.Name).Where(n => !n.StartsWith(TRANSPARENT_PREFIX)).ToList();
            scopeParam = Expression.Parameter(typeof(Scope), "scope");
            var newExpression =
                Expression.Lambda(
                    lambda.Body,
                    new ParameterExpression[]
                    {
                        scopeParam
                    }
               );
            return (LambdaExpression)base.Visit(newExpression);
        }

        /// <summary>
        /// We only want to rewrite transparent identifier accessors.
        /// </summary>
        protected override Expression VisitMember(MemberExpression node)
        {
            if (ParseExpression.IsFromTransparent(node) && !ParseExpression.IsTransparentMember(node))
            {
                var memberType = ParseExpression.GetTransMemberType(node);
                var memberName = node.Member.Name;
                return RewritePropertyAccess(node);
            }
            return base.VisitMember(node);
        }

        /// <summary>
        /// Rewrites transparent identifier accessors.
        /// </summary>
        /// <remarks>
        /// If we have a nested accessor, like:
        /// > ti0.ti1.a.b.c
        /// this will rewrite the expression as:
        /// > SCOPE.a.b.c
        /// </remarks>
        private Expression RewritePropertyAccess(MemberExpression node)
        {
            Expression current = node;
            var expressionStack = new Stack<Expression>();
            while (current.NodeType == ExpressionType.MemberAccess)
            {
                var memberAccess = ((MemberExpression)current);
                if (!memberAccess.Member.Name.StartsWith(TRANSPARENT_PREFIX)) expressionStack.Push(current);
                current = memberAccess.Expression;
            }

            var property = (MemberExpression)expressionStack.Pop();
            var propertyName = property.Member.Name;

            var value = Expression.Call(scopeParam, GetValue, Expression.Constant(PrefixedVariable(BlockCount, propertyName))); 
            Expression rewritten = Expression.Convert(value, property.Type);

            while (expressionStack.Any())
            {
                var top = expressionStack.Pop();
                rewritten = Expression.MakeMemberAccess(rewritten, ((MemberExpression)top).Member);
            }
            return rewritten;
        }

        /// <summary>
        /// Rewrites parameter accessors to read from scope.
        /// </summary>
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (paramNames.Contains(node.Name))
            {
                var memberType = node.Type;
                var memberName = node.Name;
                var value = Expression.Call(scopeParam, GetValue, Expression.Constant(PrefixedVariable(BlockCount, memberName)));
                return Expression.Convert(value, memberType);
            }
            return node;
        }

    }
}
