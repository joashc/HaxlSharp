using System.Collections.Generic;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// The variables in an expression.
    /// </summary>
    public class ExpressionVariables
    {
        public readonly bool BindsNonTransparentParam;
        public readonly List<string> Bound;
        public readonly List<string> ParameterNames;

        public ExpressionVariables(bool bindsNonTransparentParam, List<string> bound, List<string> parameterNames)
        {
            BindsNonTransparentParam = bindsNonTransparentParam;
            Bound = bound;
            ParameterNames = parameterNames;
        }
    }
}
