using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class Variables
    {
        public readonly List<FreeVariable> Free;
        public readonly List<string> Bound;
        public readonly List<string> ParameterNames;

        public Variables(List<FreeVariable> free, List<string> bound, List<string> parameterNames)
        {
            Free = free;
            Bound = bound;
            ParameterNames = parameterNames;
        }
    }

    public class FreeVariable
    {
        public readonly bool FromTransparent;
        public readonly string Name;
        public FreeVariable(string name, bool fromTransparent)
        {
            Name = name;
            FromTransparent = fromTransparent;
        }
    }

    public static class DetectApplicative
    {
        /// <summary>
        /// Checks if a given monadic bind can be expressed as an applicative functor.
        /// </summary>
        public static ApplicativeInfo CheckApplicative<A, B>(Expression<Func<A, Fetch<B>>> bind, Dictionary<string, object> previouslyBound)
        {
            var visitor = new ExpressionVarVisitor();
            visitor.Visit(bind);

            var freeVariables = visitor.Parameters.SelectMany(MemberNames);
            var transparents = freeVariables.Where(v => v.FromTransparent).GroupBy(v => v.Name).Select(grp => grp.First().Name);
            var frees = freeVariables.Where(v => !v.FromTransparent).Select(v => v.Name);
            var newFrees = frees.Concat(transparents);
            var boundVariables = visitor.Arguments.Select(m => m.Member.Name);

            var freeWithoutPrevious = newFrees.Where(v => !previouslyBound.Keys.Contains(v));

            var isApplicative = boundVariables.All(bound => !freeWithoutPrevious.Contains(bound))
                && freeWithoutPrevious.Distinct().Count() == freeWithoutPrevious.Count();

            return new ApplicativeInfo(isApplicative, freeWithoutPrevious.Concat(previouslyBound.Keys), freeWithoutPrevious.First());
        }

        public static Variables GetExpressionVariables(LambdaExpression bind)
        {
            var visitor = new ExpressionVarVisitor();
            visitor.Visit(bind.Body);
            var frees = visitor.Parameters.SelectMany(MemberNames).ToList();
            var bound = visitor.Arguments.Select(MemberAccess).Where(m => !m.Name.StartsWith("<>h__Trans") && m.FromTransparent).Select(f => f.Name).ToList();
            var paramVisitor = new ExpressionVarVisitor();
            foreach (var param in bind.Parameters)
            {
                paramVisitor.Visit(param);
            }
            return new Variables(frees, bound, paramVisitor.Parameters.SelectMany(MemberNames).Select(f => f.Name).ToList());
        }

        public static string GetFirstParameterName(LambdaExpression expr)
        {
            var firstParam = expr.Parameters.FirstOrDefault();
            if (firstParam == null) throw new ArgumentException("Lambda expression has no parameters");
            return firstParam.Name;
        }

        private static IEnumerable<FreeVariable> MemberNames(ParameterExpression parameter)
        {
            // Transparent identifiers start with this prefix
            // If we have a transparent identifier, we pull out the appropriate members.
            if (parameter.Name.StartsWith("<>h__Trans"))
            {
                var members = parameter.Type.GetMembers();
                return from member in members
                       where member.MemberType == System.Reflection.MemberTypes.Property && !member.Name.StartsWith("<>h__Trans")
                       select new FreeVariable(member.Name, true);
            }
            return new List<FreeVariable> { new FreeVariable(parameter.Name, false) };
        }

        public static bool FromTransparent(MemberExpression expression)
        {
            if (expression.Expression == null) return false;
            if (expression.Expression.NodeType == ExpressionType.Parameter)
            {
                return ((ParameterExpression)expression.Expression).Name.StartsWith("<>h__Trans");
            }
            if (expression.Expression.NodeType == ExpressionType.Constant)
            {
                return false;
            }
            return FromTransparent(expression.Expression as MemberExpression);
        }

        public static Type GetTransMemberType(MemberExpression expression)
        {
            if (!FromTransparent(expression)) throw new ArgumentException("Must be called on transparent member accessor");
            dynamic member = expression.Member;
            return member.PropertyType;
        }

        public static bool IsTransparentMember(MemberExpression expression)
        {
            return expression.Member.Name.StartsWith("<>h__Trans");
        }

        private static FreeVariable MemberAccess(MemberExpression argument)
        {
            return new FreeVariable(argument.Member.Name, FromTransparent(argument));
        }


    }
}
