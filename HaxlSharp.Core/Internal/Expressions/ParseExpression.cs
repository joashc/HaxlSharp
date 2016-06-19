using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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
            var visitor = new ParameterAccessVisitor();
            visitor.Visit(bind.Body);
            var bound = visitor.MemberAccesses
                               .Select(MemberAccess)
                               .Where(m => !m.Name.StartsWith(TRANSPARENT_PREFIX) && m.FromTransparent)
                               .Select(f => f.Name)
                               .ToList();

            var paramVisitor = new ParameterAccessVisitor();
            foreach (var param in bind.Parameters)
            {
                paramVisitor.Visit(param);
            }
            var bindsNonTransparent = bind.Parameters.Any(
                bindParam =>
                    !IsTransparent(bindParam) && BindsNonTransparentParam(visitor.ParameterAccesses, bindParam.Name));
            return new ExpressionVariables(bindsNonTransparent, bound, paramVisitor.ParameterAccesses.SelectMany(MemberNames).Select(f => f.Name).ToList());
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
                var properties = parameter.Type.GetRuntimeProperties();
                return from property in properties
                       where !property.Name.StartsWith(TRANSPARENT_PREFIX)
                       select new FreeVariable(property.Name, true);
            }
            return new List<FreeVariable> { new FreeVariable(parameter.Name, false) };
        }

        private static bool BindsNonTransparentParam(List<ParameterExpression> parameterExpressions, string paramName)
        {
            return parameterExpressions.Any(pe => pe.Name == paramName);
        }


        /// <summary>
        /// Checks if a given member expression ultimately points to a transparent identifier.
        /// </summary>
        public static bool IsFromTransparent(MemberExpression expression)
        {
            if (expression.Expression == null) return false;
            switch (expression.Expression.NodeType)
            {
                case ExpressionType.Parameter:
                    return IsTransparent((ParameterExpression)expression.Expression);
                case ExpressionType.Constant:
                    return false;
            }
            return IsFromTransparent(expression.Expression as MemberExpression);
        }


        public static bool IsTransparent(ParameterExpression expression)
        {
            return expression.Name.StartsWith(TRANSPARENT_PREFIX);
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
            //return new FreeVariable(argument.Member.Name, false);
            var fromTransparent = IsFromTransparent(argument);
            if (!fromTransparent) return new FreeVariable(argument.Member.Name, false);
            switch (argument.Expression.NodeType)
            {
                case ExpressionType.Parameter:
                    return new FreeVariable(argument.Member.Name, true);
                case ExpressionType.MemberAccess:
                {

                    var member = (MemberExpression) argument.Expression;
                    return member.Member.Name.StartsWith(TRANSPARENT_PREFIX) 
                            ? new FreeVariable(argument.Member.Name, true) 
                            : MemberAccess(member);
                }
            }
            throw new ArgumentException("Error getting transparent identifier name");
        }
    }
}
