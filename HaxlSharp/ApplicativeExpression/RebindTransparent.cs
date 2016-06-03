using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class RebindTransparent : ExpressionVisitor
    {
        private List<string> paramNames;
        private ParameterExpression scopeParam;
        public int BlockCount { get; set; }
        private MethodInfo GetValue = typeof(Scope).GetMethod("GetValue");

        public LambdaExpression Rewrite(LambdaExpression lambda)
        {
            paramNames = lambda.Parameters.Select(p => p.Name).Where(n => !n.StartsWith("<>h__Trans")).ToList();
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

        protected override Expression VisitMember(MemberExpression node)
        {
            if (DetectApplicative.FromTransparent(node) && !DetectApplicative.IsTransparentMember(node))
            {
                var memberType = DetectApplicative.GetTransMemberType(node);
                var memberName = node.Member.Name;
                return RewritePropertyAccess(node);
            }
            return base.VisitMember(node);
        }

        private Expression RewritePropertyAccess(MemberExpression node)
        {
            Expression current = node;
            var expressionStack = new Stack<Expression>();
            while (current.NodeType == ExpressionType.MemberAccess)
            {
                var memberAccess = ((MemberExpression)current);
                if (!memberAccess.Member.Name.StartsWith("<>h__Trans")) expressionStack.Push(current);
                current = memberAccess.Expression;
            }

            var property = (MemberExpression)expressionStack.Pop();
            var propertyName = property.Member.Name;

            var value = Expression.Call(scopeParam, GetValue, Expression.Constant($"{BlockCount}{propertyName}")); 
            Expression rewritten = Expression.Convert(value, property.Type);

            while (expressionStack.Any())
            {
                var top = expressionStack.Pop();
                rewritten = Expression.MakeMemberAccess(rewritten, ((MemberExpression)top).Member);
            }
            return rewritten;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (paramNames.Contains(node.Name))
            {
                var memberType = node.Type;
                var memberName = node.Name;
                var value = Expression.Call(scopeParam, GetValue, Expression.Constant($"{BlockCount}{memberName}"));
                return Expression.Convert(value, memberType);
            }
            return node;
        }

    }
}
