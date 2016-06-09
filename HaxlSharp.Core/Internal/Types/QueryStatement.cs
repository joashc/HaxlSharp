using System;
using System.Linq.Expressions;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// Represents a line in a query expression. 
    /// </summary>
    public interface QueryStatement
    {
        X Match<X>(Func<BindProjectStatement, X> bind, Func<LetStatement, X> let);
        int BlockNumber { get; set; }
        bool StartsBlock { get; set; }
        bool IsFinal { get; set; }
    }

    /// <summary>
    /// The variables of each expression in a (bind, project) expression pair.
    /// </summary>
    public class BindProjectStatement : QueryStatement
    {
        public readonly BindProjectPair Expressions;
        public readonly ExpressionVariables BindVariables;
        public readonly ExpressionVariables ProjectVariables;

        public BindProjectStatement(BindProjectPair expressions, ExpressionVariables bindVars, ExpressionVariables projectVars)
        {
            Expressions = expressions;
            BindVariables = bindVars;
            ProjectVariables = projectVars;
        }

        public X Match<X>(Func<BindProjectStatement, X> bind, Func<LetStatement, X> let)
        {
            return bind(this);
        }

        public int BlockNumber { get; set; }
        public bool StartsBlock { get; set; }
        public bool IsFinal { get; set; }
    }

    /// <summary>
    /// A let statement, e.g. 
    /// > from x in a
    /// > let y = 2
    /// > select x + y;
    /// </summary>
    public class LetStatement : QueryStatement
    {
        public readonly LambdaExpression Expression;
        public readonly ExpressionVariables Variables;
        public readonly string Name;
        public LetStatement(string name, LambdaExpression expression, ExpressionVariables variables)
        {
            Name = name;
            Expression = expression;
            Variables = variables;
        }

        public X Match<X>(Func<BindProjectStatement, X> bind, Func<LetStatement, X> let)
        {
            return let(this);
        }

        public int BlockNumber { get; set; }
        public bool StartsBlock { get; set; }
        public bool IsFinal { get; set; }
    }
}