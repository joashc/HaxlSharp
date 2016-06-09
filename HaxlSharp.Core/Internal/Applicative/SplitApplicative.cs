using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using static HaxlSharp.Internal.Base;
using System.Text.RegularExpressions;

namespace HaxlSharp.Internal
{
    public class SplitApplicative
    {
        /// <summary>
        /// Splits a monadic bind into applicative groups. 
        /// </summary>
        public static List<ApplicativeGroup> SplitBind(IEnumerable<BindProjectPair> collectedExpressions,
            LambdaExpression initial)
        {
            var vars = collectedExpressions.Select(GetVariables).ToList();
            var numbered = NumberBlocks(vars).ToList();
            return MakeApplicative(initial, numbered);
        }

        /// <summary>
        /// Gets the variables in a (bind, project) pair.
        /// </summary>
        private static QueryStatement GetVariables(BindProjectPair pair)
        {
            var project = pair.Project;
            var isLet = project.Parameters.Any(param => param.Name.StartsWith(LET_PREFIX));
            if (isLet)
            {
                var letParam = project.Parameters.ElementAt(1);
                var letName = letParam.Name;
                var originalName = Regex.Split(letName, LET_PREFIX)[1];
                var letExpression = Expression.Lambda(project.Body, project.Parameters.First(),
                    Expression.Parameter(letParam.Type, originalName));
                var letVariables = ParseExpression.GetExpressionVariables(letExpression);
                return new LetStatement(originalName, letExpression, letVariables);
            }
            var bindVars = ParseExpression.GetExpressionVariables(pair.Bind);
            var projectVars = ParseExpression.GetExpressionVariables(project);
            return new BindProjectStatement(pair, bindVars, projectVars);
        }


        /// <summary>
        /// Appends numbers indicating the block a statement belongs to.
        /// <remarks>
        /// A statement written in the format:
        /// 
        /// > var nested = from x in a
        /// >              from y in b
        /// >              select x + y;
        /// >
        /// > var fetch = from x in nested
        /// >             from y in b
        /// >             select x + y;
        /// 
        /// will be rewritten as:
        /// 
        /// > a.SelectMany(
        /// >   x => b,
        /// >   x, y => x + y
        /// > ).SelectMany(
        /// >   x => b
        /// >   x, y =>  x + y
        /// > );
        /// 
        /// Because there are two variables named "x", and these 
        /// expressions are written inline, we need to number them,
        /// in case they end up in the same applicative group.
        /// </remarks>
        /// </summary>
        public static IEnumerable<QueryStatement> NumberBlocks(List<QueryStatement> statements)
        {
            var blockNumber = 0;
            var statementCounter = 0;
            var numStatements = statements.Count();
            var isFirst = true;
            foreach (var statement in statements)
            {
                statementCounter++;
                // SplitBind functions that take one non-transparent parameter are binding the entire result from another monad.
                // This will hide any variables that were in scope in that monad.
                statement.StartsBlock = isFirst;
                if (statement.Match(
                    bind => bind.BindVariables.ParameterNames.Count == 1
                            && !ParseExpression.IsTransparent(bind.Expressions.Bind.Parameters.First()),
                    let => false)
                    )
                {
                    blockNumber++;
                    statement.StartsBlock = true;
                }
                statement.BlockNumber = blockNumber;
                if (statementCounter == numStatements) statement.IsFinal = true;
                isFirst = false;
                yield return statement;
            }
        }

        /// <summary>
        /// Groups statements that can be fetched concurrently.
        /// </summary>
        public static List<ApplicativeGroup> MakeApplicative(LambdaExpression initial,
            IEnumerable<QueryStatement> statements)
        {
            var applicatives = new List<ApplicativeGroup>();
            var currentlyBinding = initial;
            ExpressionVariables currentlyBindingVars = null;
            var currentApplicative = new List<Statement>();
            string bindParam = null;
            var boundInGroup = new List<string>();

            Action split = () =>
            {
                if (currentApplicative.Any()) applicatives.Add(new ApplicativeGroup(currentApplicative));
                currentApplicative = new List<Statement>();
                boundInGroup.Clear();
            };

            var first = true;

            foreach (var statement in statements)
            {
                var blockNumber = statement.BlockNumber;
                var accessPrevious = !first && statement.StartsBlock ? 1 : 0;
                Func<LambdaExpression, string, BoundExpression> boundExpression =
                    (e, s) => new BoundExpression(e, s, blockNumber - accessPrevious);
                statement.Match(
                    bind =>
                    {
                        var bindName = bind.BindVariables.ParameterNames.First();
                        var prefixed = PrefixedVariable(blockNumber, bindName);

                        if (first)
                        {
                            currentApplicative.Add(new BindStatement(boundExpression(currentlyBinding, prefixed)));
                        }

                        bindParam = PrefixedVariable(blockNumber, bind.ProjectVariables.ParameterNames.Last());
                        var shouldSplit = ShouldSplit(bind.BindVariables, boundInGroup);
                        if (shouldSplit) split();

                        if (bind.StartsBlock && !first)
                        {
                            var splitBlock = currentlyBindingVars != null
                                             && ShouldSplit(currentlyBindingVars, boundInGroup);
                            if (splitBlock) split();
                            boundInGroup.Clear();
                            currentApplicative.Add(
                                new ProjectStatement(boundExpression(currentlyBinding, prefixed)));
                            if (shouldSplit) split();
                        }

                        currentApplicative.Add(new BindStatement(boundExpression(bind.Expressions.Bind, bindParam)));

                        if (bind.IsFinal)
                        {
                            split();
                            currentApplicative.Add(
                                new ProjectStatement(new BoundExpression(bind.Expressions.Project, HAXL_RESULT_NAME,
                                    blockNumber)));
                        }

                        currentlyBinding = bind.Expressions.Project;
                        currentlyBindingVars = bind.ProjectVariables;
                        boundInGroup.AddRange(bind.ProjectVariables.ParameterNames);
                        return Base.Unit;
                    },
                    let =>
                    {
                        var paramNames = let.Variables.ParameterNames;
                        var bindName = paramNames.First();
                        var prefixed = PrefixedVariable(blockNumber, bindName);
                        var splitBefore = currentlyBindingVars != null && ShouldSplit(currentlyBindingVars, boundInGroup);
                        if (splitBefore) split();
                        if (first) currentApplicative.Add(new BindStatement(boundExpression(currentlyBinding, prefixed)));
                        else if (!currentlyBinding.Parameters.Any(ParseExpression.IsTransparent)) currentApplicative.Add(new ProjectStatement(boundExpression(currentlyBinding, prefixed)));


                        boundInGroup.Add(let.Variables.ParameterNames.First());
                        var shouldSplit = ShouldSplit(let.Variables, boundInGroup);
                        if (shouldSplit)
                        {
                            split();
                        }
                        boundInGroup.Add(let.Name);
                        currentApplicative.Add(
                            new ProjectStatement(boundExpression(let.Expression, PrefixedVariable(blockNumber, let.Name))));
                        return Base.Unit;
                    }
                    );
                first = false;
            }
            if (currentApplicative.Any()) applicatives.Add(new ApplicativeGroup(currentApplicative));
            return applicatives;
        }

        /// <summary>
        /// Checks if this expression binds any variables bound in the current group.
        /// </summary>
        private static bool ShouldSplit(ExpressionVariables vars, List<string> boundInGroup)
        {
            if (vars.BindsNonTransparentParam) return true;
            if (vars.Bound.Any(boundInGroup.Contains)) return true;
            return false;
        }

    }
}
