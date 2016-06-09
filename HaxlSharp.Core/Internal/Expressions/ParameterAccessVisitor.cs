using System.Collections.Generic;
using System.Linq.Expressions;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// Recursively collects the parameter and member accesses of a given expression.
    /// </summary>
    public class ParameterAccessVisitor : ExpressionVisitor
    {
        public readonly List<ParameterExpression> ParameterAccesses;
        public readonly List<MemberExpression> MemberAccesses;
        public ParameterAccessVisitor()
        {
            ParameterAccesses = new List<ParameterExpression>();
            MemberAccesses = new List<MemberExpression>();
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            ParameterAccesses.Add(node);
            return base.VisitParameter(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            MemberAccesses.Add(node);
            return base.VisitMember(node);
        }
    }

}
