using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class ExpressionArguments : ExpressionVisitor
    {
        public readonly List<MemberExpression> arguments;
        public readonly List<ParameterExpression> parameters;
        public ExpressionArguments()
        {
            arguments = new List<MemberExpression>();
            parameters = new List<ParameterExpression>();
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            arguments.Add(node);
            return base.VisitMember(node);
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            return base.VisitLambda<T>(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            parameters.Add(node);
            return base.VisitParameter(node);
        }
    }

}
