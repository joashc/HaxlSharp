using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace HaxlSharp.Internal
{
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
        public List<BoundExpression> BoundExpressions;
        public readonly bool IsProjectGroup;
    }
}
