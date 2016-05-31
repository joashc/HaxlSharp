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
        private ParameterExpression boundVariablesParameter;
        public LambdaExpression Rewrite(LambdaExpression lambda)
        {
            paramNames = lambda.Parameters.Select(p => p.Name).Where(n => !n.StartsWith("<>h__Trans")).ToList();
            boundVariablesParameter = Expression.Parameter(typeof(Dictionary<string, object>), "boundVars");
            var newExpression = 
                Expression.Lambda(
                    lambda.Body, 
                    new ParameterExpression[]
                    {
                        boundVariablesParameter
                    }
               );
            return (LambdaExpression) base.Visit(newExpression);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (DetectApplicative.FromTransparent(node) && !DetectApplicative.IsTransparentMember(node))
            {
                var memberType = DetectApplicative.GetTransMemberType(node);
                var memberName = node.Member.Name;
                var result = Expression.Property(boundVariablesParameter, "Item", Expression.Constant(memberName));
                return Expression.Convert(result, memberType);
            }
            return base.VisitMember(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (paramNames.Contains(node.Name))
            {
                var memberType = node.Type;
                var memberName = node.Name;
                var result = Expression.Property(boundVariablesParameter, "Item", Expression.Constant(memberName));
                return Expression.Convert(result, memberType);
            }
            return boundVariablesParameter;
        }

    }
}
