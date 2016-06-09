using System;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// Represents an individual statement expression.
    /// </summary>
    public interface Statement
    {
        X Match<X>(
            Func<BindStatement, X> bind,
            Func<ProjectStatement, X> project
        );
    }

    /// <summary>
    /// A statement that returns a monad type.
    /// </summary>
    public class BindStatement : Statement
    {
        public readonly BoundExpression Expression;

        public BindStatement(BoundExpression expression)
        {
            Expression = expression;
        }

        public X Match<X>(Func<BindStatement, X> bind, Func<ProjectStatement, X> project)
        {
            return bind(this);
        }
    }

    /// <summary>
    /// A statement that returns a non-monadic type.
    /// </summary>
    public class ProjectStatement : Statement
    {
        public readonly BoundExpression Expression;
        public ProjectStatement(BoundExpression expression)
        {
            Expression = expression;
        }

        public X Match<X>(Func<BindStatement, X> bind, Func<ProjectStatement, X> project)
        {
            return project(this);
        }
    }
}
