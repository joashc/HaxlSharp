using System.Linq.Expressions;

namespace HaxlSharp
{
    /// <summary>
    /// A pair of (bind, project) expressions that form part of a query expression.
    /// </summary>
    /// <remarks>
    /// Given the following query:
    /// 
    /// > from x in a       // (1)
    /// > from y in b(x)    // (2)
    /// > from z in c(x)    // (3)
    /// > select (x, y, z)  // (4)
    /// 
    /// It will be desugared into:
    ///
    /// a.SelectMany(                       // Line (1) Initial
    ///     x => b,                         // Line (2) Bind 
    ///     (x, y) => new { x, y }          // Line (2) Project
    ///  )     
    ///  .SelectMany(
    ///     ti0 => c(ti0.x),                // Line (3) Bind
    ///     (ti0, z) => (ti0.x, ti0.y, z)   // Line (4) Final Project
    ///  );
    /// 
    /// We can represent any query expression as an initial expression, along with a list of (bind, project) pairs.
    /// </remarks>
    public class BindProjectPair
    {
        public BindProjectPair(LambdaExpression bind, LambdaExpression project)
        {
            Bind = bind;
            Project = project;
        }

        public readonly LambdaExpression Bind;
        public readonly LambdaExpression Project;
    }
}