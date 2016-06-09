using System;
using System.Collections.Generic;
using System.Linq;
using static HaxlSharp.Internal.Base;

namespace HaxlSharp.Internal
{
    public static class HaxlApplicative
    {
        /// <summary>
        /// Converts a project expression to Haxl monad.
        /// </summary>
        public static Func<Scope, Haxl> ProjectToHaxl(ProjectStatement project, string parentBind)
        {
            return scope => Haxl.FromFunc(cache =>
            {
                var rewritten = RebindToScope.Rebind(project.Expression);
                var result = rewritten.Compile().DynamicInvoke(scope);
                return Done.New(_ =>
                {
                    if (project.Expression.BindVariable == HAXL_RESULT_NAME
                        && !scope.IsRoot
                        && parentBind != null)
                    {
                        return scope.WriteParent(parentBind, result);
                    }
                    return scope.Add(project.Expression.BindVariable, result);
                });
            });
        }

        /// <summary>
        /// Converts a single bind expression to the Haxl monad.
        /// </summary>
        /// <param name="bind"></param>
        /// <returns></returns>
        public static Func<Scope, Haxl> BindToHaxl(BindStatement bind)
        {
            return scope =>
            {
                var rewritten = RebindToScope.Rebind(bind.Expression);
                var value = rewritten.Compile().DynamicInvoke(scope);
                var wrapped = (Fetchable)value;
                return wrapped.ToHaxlFetch(bind.Expression.BindVariable, scope);
            };
        }

        /// <summary>
        /// Converts to Haxl monad, dispatching on statement type.
        /// </summary>
        public static Func<Scope, Haxl> StatementToHaxl(Statement statement, string parentBind)
        {
            return statement.Match(
                BindToHaxl,
                project => ProjectToHaxl(project, parentBind));
        }

        /// <summary>
        /// Folds an applicative group into a Haxl monad.
        /// </summary>
        public static Func<Scope, Haxl> ApplicativeToHaxl(ApplicativeGroup applicative, string parentBind)
        {
            var expressions = applicative.Expressions;
            if (applicative.Expressions.Count == 1) return StatementToHaxl(expressions.First(), parentBind);
            return scope => applicative.Expressions.Aggregate
                (
                    Haxl.FromFunc(c => Done.New(s => s)),
                    (group, be) =>
                    {
                        var haxl = StatementToHaxl(be, parentBind)(scope);
                        return group.Applicative(haxl);
                    }
                );
        }

        /// <summary>
        /// Converts a list of applicative groups into a Haxl monad.
        /// </summary>
        public static Haxl ToFetch(List<ApplicativeGroup> split, string parentBind, Scope parentScope)
        {
            if (parentScope == null) parentScope = Scope.New();
            Haxl finalFetch = null;
            Action<Func<Scope, Haxl>> bindToFinal = f =>
            {
                finalFetch = finalFetch == null ? f(parentScope) : finalFetch.Bind(f);
            };

            foreach (var applicative in split)
            {
                bindToFinal(ApplicativeToHaxl(applicative, parentBind));
            }
            return finalFetch;
        }

    }
}
