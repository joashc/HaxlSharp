using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using static HaxlSharp.Internal.Base;

namespace HaxlSharp.Internal
{
    public static class ParseExpression
    {
        /// <summary>
        /// Gets the free and bound variables of a lambda expression.
        /// </summary>
        public static ExpressionVariables GetExpressionVariables(LambdaExpression bind)
        {
            var visitor = new ExpressionVarVisitor();
            visitor.Visit(bind.Body);
            var frees = visitor.Parameters.SelectMany(MemberNames).ToList();
            var bound = visitor.Arguments.Select(MemberAccess).Where(m => !m.Name.StartsWith(TRANSPARENT_PREFIX) && m.FromTransparent).Select(f => f.Name).ToList();
            var paramVisitor = new ExpressionVarVisitor();
            foreach (var param in bind.Parameters)
            {
                paramVisitor.Visit(param);
            }
            return new ExpressionVariables(frees, bound, paramVisitor.Parameters.SelectMany(MemberNames).Select(f => f.Name).ToList());
        }

        /// <summary>
        /// Get all member names within a parameter expression.
        /// </summary>
        private static IEnumerable<FreeVariable> MemberNames(ParameterExpression parameter)
        {
            // Transparent identifiers start with this prefix
            // If we have a transparent identifier, we pull out the appropriate members.
            if (parameter.Name.StartsWith(TRANSPARENT_PREFIX))
            {
                var members = parameter.Type.GetMembers();
                return from member in members
                       where member.MemberType == System.Reflection.MemberTypes.Property && !member.Name.StartsWith(TRANSPARENT_PREFIX)
                       select new FreeVariable(member.Name, true);
            }
            return new List<FreeVariable> { new FreeVariable(parameter.Name, false) };
        }

        /// <summary>
        /// Checks if a given member expression ultimately points to a transparent identifier.
        /// </summary>
        public static bool IsFromTransparent(MemberExpression expression)
        {
            if (expression.Expression == null) return false;
            if (expression.Expression.NodeType == ExpressionType.Parameter)
            {
                return ((ParameterExpression)expression.Expression).Name.StartsWith(TRANSPARENT_PREFIX);
            }
            if (expression.Expression.NodeType == ExpressionType.Constant)
            {
                return false;
            }
            return IsFromTransparent(expression.Expression as MemberExpression);
        }

        /// <summary>
        /// 
        /// </summary>
        public static Type GetTransMemberType(MemberExpression expression)
        {
            if (!IsFromTransparent(expression)) throw new ArgumentException("Must be called on transparent member accessor");
            dynamic member = expression.Member;
            return member.PropertyType;
        }

        /// <summary>
        /// 
        /// </summary>
        public static bool IsTransparentMember(MemberExpression expression)
        {
            return expression.Member.Name.StartsWith(TRANSPARENT_PREFIX);
        }

        /// <summary>
        /// 
        /// </summary>
        private static FreeVariable MemberAccess(MemberExpression argument)
        {
            return new FreeVariable(argument.Member.Name, IsFromTransparent(argument));
        }
    }
}
