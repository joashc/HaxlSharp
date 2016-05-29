using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    /// <summary>
    /// Recursively collects the arguments and parameters of a given expression.
    /// </summary>
    public class ExpressionVariables : ExpressionVisitor
    {
        public readonly List<MemberExpression> Arguments;
        public readonly List<ParameterExpression> Parameters;
        public ExpressionVariables()
        {
            Arguments = new List<MemberExpression>();
            Parameters = new List<ParameterExpression>();
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            Arguments.Add(node);
            return base.VisitMember(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            return base.VisitBinary(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Parameters.Add(node);
            return base.VisitParameter(node);
        }
    }

}
