using System.Linq.Expressions;

namespace HaxlSharp
{
    /// <summary>
    /// An expression that is bound to a particular variable name.
    /// </summary>
    public class BoundExpression
    {
        public readonly LambdaExpression Expression;
        public readonly string BindVariable;
        public BoundExpression(LambdaExpression expression, string bindVariable)
        {
            Expression = expression;
            BindVariable = bindVariable;
        }
    }
}
