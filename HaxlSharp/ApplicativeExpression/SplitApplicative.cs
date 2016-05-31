﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class SplitApplicatives<A>
    {
        public readonly Expr<A> Expression;
        public readonly IEnumerable<ApplicativeGroup> Segments;
        public readonly bool IsIdentity;
        public readonly Queue<string> NameQueue;
        public readonly LambdaExpression FinalProject;

        public SplitApplicatives(Expr<A> expression, IEnumerable<ApplicativeGroup> segments, Queue<string> nameQueue, LambdaExpression finalProject)
        {
            Expression = expression;
            Segments = segments;
            NameQueue = nameQueue;
            IsIdentity = false;
            FinalProject = finalProject;
        }

        public SplitApplicatives(Expr<A> expression)
        {
            IsIdentity = true;
            Expression = expression;
        }
    }

    public class ExpressionVariables
    {
        public readonly BindProjectPair Expressions;
        public readonly Variables BindVariables;
        public readonly Variables ProjectVariables;

        public ExpressionVariables(BindProjectPair expressions, Variables bindVars, Variables projectVars)
        {
            Expressions = expressions;
            BindVariables = bindVars;
            ProjectVariables = projectVars;
        }
    }

    public static class Splitter
    {
        public static SplitApplicatives<A> Split<A>(Expr<A> expression)
        {
            if (IsIdentity(expression)) return new SplitApplicatives<A>(expression);
            var segments = new List<ApplicativeGroup>();
            var currentSegment = new ApplicativeGroup();
            var seenParameters = new HashSet<string>();

            // Initialize first applicative group
            currentSegment.Expressions.Add(expression.Initial);

            var vars = expression.CollectedExpressions.Select(GetVariables);
            var boundParams = vars.SelectMany(v => v.BindVariables.ParameterNames);
            var projectedParams = vars.Select(v => v.ProjectVariables.ParameterNames);
            var boundVarQueue = BoundQueryVars(projectedParams);

            foreach (var expr in vars)
            {
                var split = ShouldSplit(expr, currentSegment.BoundVariables, seenParameters);
                currentSegment.BoundVariables.AddRange(expr.BindVariables.ParameterNames);
                if (split)
                {
                    segments.Add(currentSegment);
                    currentSegment = new ApplicativeGroup();
                }
                currentSegment.Expressions.Add(expr.Expressions.Bind);
                if (expr.ProjectVariables.Free.Any(f => !boundParams.Contains(f.Name))) currentSegment.Expressions.Add(expr.Expressions.Project);
            }
            var finalProject = currentSegment.Expressions.Last();
            currentSegment.Expressions.RemoveAt(currentSegment.Expressions.Count - 1);
            segments.Add(currentSegment);
            return new SplitApplicatives<A>(expression, segments, boundVarQueue, finalProject);
        }

        public static bool IsIdentity<A>(Expr<A> expression)
        {
            return expression is Identity<A>;
        }

        private static string GetBindParameter(ExpressionVariables vars, HashSet<string> seenParams)
        {
            return vars.BindVariables.ParameterNames.First(name => !seenParams.Contains(name));
        }

        private static bool ShouldSplit(ExpressionVariables vars, List<string> boundInGroup, HashSet<string> seenParams)
        {
            var firstParamName = GetBindParameter(vars, seenParams);
            seenParams.Add(firstParamName);
            if (vars.BindVariables.Free.Where(f => !f.FromTransparent).Any(f => f.Name == firstParamName)) return true;
            if (vars.BindVariables.Bound.Contains(firstParamName)) return true;
            if (vars.BindVariables.Bound.Any(b => boundInGroup.Contains(b))) return true;
            return false;
        }

        private static ExpressionVariables GetVariables(BindProjectPair pair)
        {
            var bindVars = DetectApplicative.GetExpressionVariables(pair.Bind);
            var projectVars = DetectApplicative.GetExpressionVariables(pair.Project);
            return new ExpressionVariables(pair, bindVars, projectVars);
        }

        private static Queue<string> BoundQueryVars(IEnumerable<IEnumerable<string>> nameGroups)
        {
            var seen = new HashSet<string>();
            var nameQueue = new Queue<string>();
            foreach (var nameGroup in nameGroups)
            {
                var unseen = nameGroup.Where(name => !seen.Contains(name));
                foreach (var param in unseen)
                {
                    seen.Add(param);
                    nameQueue.Enqueue(param);
                }
            }
            return nameQueue;
        }
    }
}
