using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class SplitApplicatives<A>
    {
        public readonly Fetch<A> Expression;
        public readonly IEnumerable<ApplicativeGroup> Segments;
        public readonly bool IsIdentity;
        public readonly Queue<string> NameQueue;
        public readonly LambdaExpression FinalProject;

        public SplitApplicatives(Fetch<A> expression, IEnumerable<ApplicativeGroup> segments, Queue<string> nameQueue, LambdaExpression finalProject)
        {
            Expression = expression;
            Segments = segments;
            NameQueue = nameQueue;
            IsIdentity = false;
            FinalProject = finalProject;
        }

        public SplitApplicatives(Fetch<A> expression)
        {
            IsIdentity = true;
            Expression = expression;
        }

    }

    public class SplitBind<A> : SplitFetch<A>
    {
        public readonly Fetch<A> Expression;
        public readonly IEnumerable<ApplicativeGroup> Segments;
        public readonly Queue<string> NameQueue;
        public readonly LambdaExpression FinalProject;

        public SplitBind(Fetch<A> expression, IEnumerable<ApplicativeGroup> segments, Queue<string> nameQueue, LambdaExpression finalProject)
        {
            Expression = expression;
            Segments = segments;
            NameQueue = nameQueue;
            FinalProject = finalProject;
        }

        public X Run<X>(SplitHandler<A, X> handler)
        {
            return handler.Bind(this);
        }

        public BlockedRequestList CollectRequests(string bindTo)
        {
            return Run(new RequestCollector<A>(bindTo));
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

    public class Splitta<C> : FetchSplitter<C>
    {
        public SplitFetch<C> Bind<A, B>(Fetch<C> bind)
        {
            return Splitter.Split(bind);
        }

        public SplitFetch<C> Pass(Fetch<C> unsplittable)
        {
            return (SplitFetch<C>) unsplittable;
        }
    }


    public static class Splitter
    {
        public static SplitFetch<A> Split<A>(Fetch<A> expression)
        {
            var segments = new List<ApplicativeGroup>();
            var currentSegment = new ApplicativeGroup();
            var seenParameters = new HashSet<string>();

            // Initialize first applicative group
            currentSegment.Expressions.Add(expression.Initial);

            var vars = expression.CollectedExpressions.Select(GetVariables);
            var boundParams = vars.SelectMany(v => v.BindVariables.ParameterNames);

            Action<bool> addCurrentSegment = isProject =>
            {
                if (!currentSegment.Expressions.Any()) return;
                segments.Add(currentSegment);
                currentSegment = new ApplicativeGroup(isProject);
            };

            var blockCount = 0;

            foreach (var expr in vars)
            {
                var split = ShouldSplit(expr, currentSegment.BoundVariables, seenParameters);
                currentSegment.BoundVariables.AddRange(expr.BindVariables.ParameterNames);
                var hasProject = expr.ProjectVariables.ParameterNames.Any(f => !boundParams.Contains(f));
                if (hasProject) {
                    currentSegment.BoundVariables.AddRange(expr.ProjectVariables.ParameterNames);
                    seenParameters.Clear();
                }

                if (split) addCurrentSegment(false);

                currentSegment.Expressions.Add(expr.Expressions.Bind);

                if (hasProject)
                {
                    blockCount++;
                    addCurrentSegment(true);
                    currentSegment.Expressions.Add(expr.Expressions.Project);
                    addCurrentSegment(false);
                }
            }

            var boundVarQueue = BoundQueryVars(segments);
            var finalProject = segments.Last().Expressions.First();
            segments.RemoveAt(segments.Count - 1);
            return new SplitBind<A>(expression, segments, boundVarQueue, finalProject);
            //return new SplitApplicatives<A>(expression, segments, boundVarQueue, finalProject);
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

        private static Queue<string> BoundQueryVars(List<ApplicativeGroup> groups)
        {
            var seen = new HashSet<string>();
            var nameQueue = new Queue<string>();
            var i = 0;
            foreach (var group in groups)
            {
                // Handle composed queries 
                if (group.IsProjectGroup) 
                {
                    i++;
                    seen.Clear();
                }
                var unseen = group.BoundVariables.Where(name => !seen.Contains(name));
                foreach (var param in unseen)
                {
                    seen.Add(param);
                    var prefixed = $"{i}{param}";
                    nameQueue.Enqueue(prefixed);
                }
            }
            return nameQueue;
        }
    }
}
