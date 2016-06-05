using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class SplitBind<A> : SplitFetch<A>
    {
        public IEnumerable<ApplicativeGroup> Segments { get; }
        public Queue<string> NameQueue { get; }

        public SplitBind(IEnumerable<ApplicativeGroup> segments, Queue<string> nameQueue)
        {
            Segments = segments;
            NameQueue = nameQueue;
        }

        public X Run<X>(SplitHandler<A, X> handler)
        {
            return handler.Bind(this);
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
            return (SplitFetch<C>)unsplittable;
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
                if (hasProject)
                {
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
            var boundVarQueue2 = BoundQueryVars(segments);
            foreach (var segment in segments)
            {
                segment.BoundExpressions = segment.Expressions.Select(e =>
                {
                    var bindVar = boundVarQueue2.Dequeue();
                    return new BoundExpression(e, bindVar);
                }).ToList();
            }
            return new SplitBind<A>(segments, boundVarQueue);
        }

        public static async Task<Scope> RunFetch(Fetch fetch, Scope scope, Fetcher fetcher)
        {
            var result = fetch.Result.Value;
            return await result.Match(
                done => Task.FromResult(done.AddToScope(scope)),
                async blocked =>
                {
                    await fetcher.FetchBatch(blocked.BlockedRequests);
                    return await RunFetch(blocked.Continue, scope, fetcher);
                }
            );
        }

        public static Fetch ToFetch<A>(SplitBind<A> split, string parentBind = null, Scope parentScope = null)
        {
            if (parentScope == null) parentScope = Scope.New();
            var rebinder = new RebindTransparent();
            Fetch finalFetch = null;
            Action<Func<Scope, Fetch>> bind = f =>
            {
                if (finalFetch == null) finalFetch = f(parentScope);
                else finalFetch = finalFetch.Bind(f);
            };

            foreach (var segment in split.Segments)
            {
                if (segment.IsProjectGroup)
                {
                    var boundProject = segment.BoundExpressions.First();
                    var rewritten = rebinder.Rewrite(boundProject.Expression);
                    var wrapped = rewritten.Compile();
                    finalFetch = finalFetch.Bind(scope =>
                    Fetch.FromFunc(() =>
                   {
                       var result = wrapped.DynamicInvoke(scope);
                       return Done.New(_ =>
                       {
                           if (boundProject.BindVariable == "<>HAXL_RESULT" && !scope.IsRoot && parentBind != null)
                           {
                               return scope.WriteParent(parentBind, result);
                           }
                           else
                           {
                               return scope.Add(boundProject.BindVariable, result);
                           }
                       });
                   }));
                    continue;
                }
                Func<Scope, Fetch> currentGroup = scope =>
                    segment.BoundExpressions.Aggregate(Fetch.FromFunc(() => Done.New(s => s)), (group, be) =>
                    {
                        var rewritten = rebinder.Rewrite(be.Expression);
                        dynamic wrapped = rewritten.Compile().DynamicInvoke(scope);
                        var fetch = (Fetch)wrapped.ToFetch(be.BindVariable, scope);
                        return group.Applicative(fetch);
                    });
                bind(currentGroup);
            }
            return finalFetch;
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
            nameQueue.Enqueue("<>HAXL_RESULT");
            return nameQueue;
        }
    }
}
