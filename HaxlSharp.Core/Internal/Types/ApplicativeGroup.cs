using System.Collections.Generic;

namespace HaxlSharp.Internal
{
    public class ApplicativeGroup
    {
        public readonly List<Statement> Expressions;

        public ApplicativeGroup(List<Statement> expressions)
        {
            Expressions = expressions;
        }
    }

}
