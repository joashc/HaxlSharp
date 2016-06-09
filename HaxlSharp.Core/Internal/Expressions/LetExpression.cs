using System;
using System.Linq;
using System.Linq.Expressions;
using static HaxlSharp.Internal.Base;

namespace HaxlSharp.Internal
{
    public static class LetExpression
    {
        /// <summary>
        /// Checks if a select expression is a Let.
        /// </summary>
        /// <remarks>
        /// This is not a reliable test; it only checks if:
        /// - The lambda body is just a new expression
        /// - It returns an anonymous type
        /// - The parameter name is the same as the first member
        /// 
        /// If we were to write a select with all these attributes:
        /// 
        /// </remarks>
        public static bool IsLetExpression(LambdaExpression expression)
        {
            if (expression.Body.NodeType != ExpressionType.New) return false;
            var newExpression = (NewExpression)expression.Body;
            if (newExpression.Arguments.Count != 2) return false;
            if (!expression.ReturnType.Name.StartsWith("<>f__AnonymousType")) return false;
            var paramName = expression.Parameters.First().Name;
            if (paramName != newExpression.Members.First().Name) return false;
            return true;
        }

        public static LambdaExpression RewriteLetExpression<A, B>(Expression<Func<A, B>> expression)
        {
            var body = (NewExpression)expression.Body;
            var letVar = body.Arguments.ElementAt(1);
            var letParam = body.Members.ElementAt(1);
            return Expression.Lambda(letVar, expression.Parameters.First(), Expression.Parameter(letVar.Type, PrefixLet(letParam.Name)));
        }
    }

}
