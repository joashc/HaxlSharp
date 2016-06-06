using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using static HaxlSharp.Haxl;
using System.Threading.Tasks;
using System.Diagnostics;

namespace HaxlSharp
{
    public class QuerySplitter
    {
        public static BindSegments Bind(IEnumerable<BindProjectPair> collectedExpressions, LambdaExpression initial)
        {
            return SplitQuery.Split(collectedExpressions, initial);
        }
    }

    public static class SplitQuery
    {
        public static BindSegments Split(IEnumerable<BindProjectPair> collectedExpressions, LambdaExpression initial)
        {
            var segments = new List<ApplicativeGroup>();
            var currentSegment = new ApplicativeGroup();
            var seenParameters = new HashSet<string>();

            // Initialize first applicative group
            currentSegment.Expressions.Add(initial);

            var vars = collectedExpressions.Select(GetVariables);
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
            foreach (var segment in segments)
            {
                segment.BoundExpressions = segment.Expressions.Select(e =>
                {
                    var bindVar = boundVarQueue.Dequeue();
                    return new BoundExpression(e, bindVar);
                }).ToList();
            }
            return new BindSegments(segments);
        }

        public static async Task<Scope> RunFetch(HaxlFetch fetch, Scope scope, Fetcher fetcher)
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

        public static HaxlFetch ToFetch(BindSegments split, string parentBind = null, Scope parentScope = null)
        {
            if (parentScope == null) parentScope = Scope.New();
            var rebinder = new RebindToScope();
            HaxlFetch finalFetch = null;
            Action<Func<Scope, HaxlFetch>> bind = f =>
            {
                if (finalFetch == null) finalFetch = f(parentScope);
                else finalFetch = finalFetch.Bind(f);
            };

            foreach (var segment in split.Segments)
            {
                if (segment.IsProjectGroup)
                {
                    var boundProject = segment.BoundExpressions.First();
                    var rewritten = rebinder.Rebind(boundProject.Expression);
                    var wrapped = rewritten.Compile();
                    finalFetch = finalFetch.Bind(scope =>
                    HaxlFetch.FromFunc(() =>
                   {
                       var result = wrapped.DynamicInvoke(scope);
                       return Done.New(_ =>
                       {
                           if (boundProject.BindVariable == HAXL_RESULT_NAME && !scope.IsRoot && parentBind != null)
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
                Func<Scope, HaxlFetch> currentGroup = scope =>
                    segment.BoundExpressions.Aggregate(HaxlFetch.FromFunc(() => Done.New(s => s)), (group, be) =>
                    {
                        var rewritten = rebinder.Rebind(be.Expression);
                        dynamic wrapped = rewritten.Compile().DynamicInvoke(scope);
                        var fetch = (HaxlFetch) wrapped.ToHaxlFetch(be.BindVariable, scope);
                        return group.Applicative(fetch);
                    });
                bind(currentGroup);
            }
            return finalFetch;
        }

        private static string GetBindParameter(BindProjectPairVars vars, HashSet<string> seenParams)
        {
            return vars.BindVariables.ParameterNames.First(name => !seenParams.Contains(name));
        }

        private static bool ShouldSplit(BindProjectPairVars vars, List<string> boundInGroup, HashSet<string> seenParams)
        {
            var firstParamName = GetBindParameter(vars, seenParams);
            seenParams.Add(firstParamName);
            if (vars.BindVariables.Free.Where(f => !f.FromTransparent).Any(f => f.Name == firstParamName)) return true;
            if (vars.BindVariables.Bound.Contains(firstParamName)) return true;
            if (vars.BindVariables.Bound.Any(b => boundInGroup.Contains(b))) return true;
            return false;
        }

        private static BindProjectPairVars GetVariables(BindProjectPair pair)
        {
            var bindVars = ParseExpression.GetExpressionVariables(pair.Bind);
            var projectVars = ParseExpression.GetExpressionVariables(pair.Project);
            return new BindProjectPairVars(pair, bindVars, projectVars);
        }

        private static Queue<string> BoundQueryVars(List<ApplicativeGroup> groups)
        {
            var seen = new HashSet<string>();
            var nameQueue = new Queue<string>();
            var blockCount = 0;
            foreach (var group in groups)
            {
                // Handle composed queries 
                if (group.IsProjectGroup)
                {
                    blockCount++;
                    seen.Clear();
                }
                var unseen = group.BoundVariables.Where(name => !seen.Contains(name));
                foreach (var param in unseen)
                {
                    seen.Add(param);
                    var prefixed = PrefixedVariable(blockCount, param);
                    nameQueue.Enqueue(prefixed);
                }
            }
            nameQueue.Enqueue(HAXL_RESULT_NAME);
            return nameQueue;
        }
    }
}
