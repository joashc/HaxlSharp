using System;
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
        public bool FirstSplit;
        public Queue<string> NameQueue;

        public SplitApplicatives(Expr<A> expression, IEnumerable<ApplicativeGroup> segments, bool firstSplit, Queue<string> nameQueue)
        {
            Expression = expression;
            Segments = segments;
            NameQueue = nameQueue;
            FirstSplit = firstSplit;
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
            var segments = new List<ApplicativeGroup>();
            var currentSegment = new ApplicativeGroup();
            var seenParameters = new HashSet<string>();

            // Initialize first applicative group
            currentSegment.Expressions.Add(expression.Initial);

            var vars = expression.CollectedExpressions.Select(GetVariables);
            var bound = vars.SelectMany(v => v.BindVariables.ParameterNames);

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
                if (expr.ProjectVariables.Free.Any(f => !bound.Contains(f.Name))) currentSegment.Expressions.Add(expr.Expressions.Project);
            }
            segments.Add(currentSegment);
            return new SplitApplicatives<A>(expression, segments, segments.First().Expressions.Count == 1, new Queue<string>());
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
            var bound = new HashSet<string>();
            var nameQueue = new Queue<string>();
            foreach (var nameGroup in nameGroups)
            {
                var newBinder = nameGroup.FirstOrDefault(name => !bound.Contains(name));
                if (newBinder == null) continue;
                bound.Add(newBinder);
                nameQueue.Enqueue(newBinder);
            }
            return nameQueue;
        }

        private static bool EnqueueBinder(List<FreeVariable> freeVars, HashSet<string> bound, Queue<string> nameQueue)
        {
            var newBinder = freeVars.FirstOrDefault(f => !bound.Contains(f.Name));
            if (newBinder == null) return false;
            bound.Add(newBinder.Name);
            nameQueue.Enqueue(newBinder.Name);
            return true;
        }
    }
}
