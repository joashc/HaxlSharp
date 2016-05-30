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
        public readonly Dictionary<string, object> boundVariables;
        public RebindTransparent(Dictionary<string, object> boundVariables)
        {
            this.boundVariables = boundVariables;
        }

        public LambdaExpression Rewrite(LambdaExpression lambda)
        {
            return (LambdaExpression) base.Visit(Expression.Lambda(lambda.Body));
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (DetectApplicative.FromTransparent(node) && !DetectApplicative.IsTransparentMember(node))
            {
                boundVariables["x"] = 100;
                boundVariables["y"] = 10;
                var memberType = DetectApplicative.GetTransMemberType(node);
                var memberName = node.Member.Name;
                var dictionary = Expression.Constant(boundVariables);
                var result = Expression.Property(dictionary, "Item", Expression.Constant(memberName));
                return Expression.Convert(result, memberType);
            }
            return base.VisitMember(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return Expression.Parameter(typeof(Dictionary<string, object>));
        }

    }
}
