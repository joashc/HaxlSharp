using System.Collections.Generic;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// The variables in an expression.
    /// </summary>
    public class ExpressionVariables
    {
        public readonly List<FreeVariable> Free;
        public readonly List<string> Bound;
        public readonly List<string> ParameterNames;

        public ExpressionVariables(List<FreeVariable> free, List<string> bound, List<string> parameterNames)
        {
            Free = free;
            Bound = bound;
            ParameterNames = parameterNames;
        }
    }
}
