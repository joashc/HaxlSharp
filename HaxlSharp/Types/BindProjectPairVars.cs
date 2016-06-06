
namespace HaxlSharp
{
    /// <summary>
    /// The variables of each expression in a (bind, project) expression pair.
    /// </summary>
    public class BindProjectPairVars
    {
        public readonly BindProjectPair Expressions;
        public readonly ExpressionVariables BindVariables;
        public readonly ExpressionVariables ProjectVariables;

        public BindProjectPairVars(BindProjectPair expressions, ExpressionVariables bindVars, ExpressionVariables projectVars)
        {
            Expressions = expressions;
            BindVariables = bindVars;
            ProjectVariables = projectVars;
        }
    }
}