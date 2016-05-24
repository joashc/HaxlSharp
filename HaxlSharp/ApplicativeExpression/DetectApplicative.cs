using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public static class DetectApplicative
    {
        /// <summary>
        /// Checks if a given monadic bind can be expressed as an applicative functor.
        /// </summary>
        public static bool IsApplicative<A, B>(Expression<Func<A, Fetch<B>>> bind)
        {
            var visitor = new ExpressionVariables();
            visitor.Visit(bind);

            var freeVariables = visitor.Parameters.SelectMany(MemberNames).ToList();
            var boundVariables = visitor.Arguments.Select(m => m.Member.Name);

            return boundVariables.All(bound => !freeVariables.Contains(bound))
                && freeVariables.Distinct().Count() == freeVariables.Count();
        }

        private static IEnumerable<string> MemberNames(ParameterExpression parameter)
        {
            // Transparent identifiers start with this prefix
            // If we have a transparent identifier, we pull out the appropriate members.
            if (parameter.Name.StartsWith("<>h__Trans"))
            {
                var members = parameter.Type.GetMembers();
                return from member in members
                       where member.MemberType == System.Reflection.MemberTypes.Property
                       select member.Name;
            }
            return new List<string> { parameter.Name };
        }

    }
}
