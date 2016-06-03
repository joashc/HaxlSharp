using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class SplitRunner<A> : SplitHandler<A, Task<A>>
    {
        public readonly Fetcher fetcher;
        public int nestLevel;
        public SplitRunner(Fetcher fetcher, int nestLevel)
        {
            this.fetcher = fetcher;
        }

        public Task<A> Bind(SplitBind<A> splits)
        {
            return RunSplits.Run(splits, fetcher, nestLevel);
        }

        public async Task<A> Request(Returns<A> request, Type requestType)
        {
            var result = await fetcher.Fetch(new BlockedRequest(request, requestType, ""));
            return (A)result.Value;
        }

        public async Task<A> RequestSequence<B, Item>(IEnumerable<B> list, Func<B, Fetch<Item>> bind)
        {
            var tasks = list.Select(b =>
            {
                var fetch = bind(b);
                var split = fetch.Split();
                return split.Run(new SplitRunner<Item>(fetcher, nestLevel + 1));
            });
            var results = await Task.WhenAll(tasks);
            return (A)(object)results.ToList();
        }

        public Task<A> Result(A result)
        {
            return Task.FromResult(result);
        }

        public async Task<A> Select<B>(Fetch<B> fetch, Expression<Func<B, A>> fmap)
        {
            var split = fetch.Split();
            var result = await split.Run(new SplitRunner<B>(fetcher, nestLevel + 1));
            return fmap.Compile()(result);
        }
    }

    public class RequestCollector<A> : SplitHandler<A, BlockedRequestList>
    {
        public readonly string bindTo;
        public RequestCollector(string bindTo)
        {
            this.bindTo = bindTo;
        }

        public BlockedRequestList Bind(SplitBind<A> splits)
        {
            return RunSplits.GetRequests(splits);
        }

        public BlockedRequestList Request(Returns<A> request, Type requestType)
        {
            var blocked = new List<FetchResult> { new BlockedRequest(request, requestType, bindTo) };
            return new BlockedRequestList(blocked, new List<ApplicativeGroup>());
        }

        public BlockedRequestList RequestSequence<B, Item>(IEnumerable<B> list, Func<B, Fetch<Item>> bind)
        {
            var blocked = list.Select(b =>
            {
                var fetch = bind(b);
                var split = fetch.Split();
                return split.CollectRequests("");
            });
            return blocked.Aggregate(BlockedRequestList.Empty(), (acc, br) => new BlockedRequestList(acc.Blocked.Concat(br.Blocked), acc.Remaining.Concat(acc.Remaining)));
        }

        public BlockedRequestList Result(A result)
        {
            var blocked = new List<FetchResult> { new ProjectResult(scope => { scope.Add(bindTo, result); }) };
            return new BlockedRequestList(blocked, new List<ApplicativeGroup>());

        }

        public BlockedRequestList Select<B>(Fetch<B> fetch, Expression<Func<B, A>> fmap)
        {
            var split = fetch.Split();
            var requests = split.CollectRequests(bindTo);
            var compiled = fmap.Compile();
            var blocked = new List<FetchResult> { new ProjectResult(scope => { scope.Add(bindTo, compiled((B)scope.GetValue(bindTo))); }) };
            return new BlockedRequestList(blocked, new List<ApplicativeGroup>());
        }
    }

    public class BlockedRequestList
    {
        public BlockedRequestList(IEnumerable<FetchResult> blocked, IEnumerable<ApplicativeGroup> remaining)
        {
            Blocked = blocked;
            Remaining = remaining;
            //NameQueue = nameQueue;
            //FinalProject = finalProject;
            //Rebinder = rebinder;
        }

        public readonly IEnumerable<FetchResult> Blocked;
        public readonly IEnumerable<ApplicativeGroup> Remaining;
        public readonly Queue<string> NameQueue;
        public readonly LambdaExpression FinalProject;
        public readonly RebindTransparent Rebinder;

        public static BlockedRequestList Empty()
        {
            return new BlockedRequestList(new List<FetchResult>(), new List<ApplicativeGroup>());
        }
    }

    public static class RunSplits
    {
        public static bool IsGenericTypeOf(Type type, Type generic)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == generic;
        }

        private static object Unwrap(object result)
        {
            var type = result.GetType();
            if (IsGenericTypeOf(type, typeof(FetchResult<>)))
            {
                return ((HoldsObject)result).Value;
            }
            return result;
        }

        //public static IEnumerable<GenericRequests> CollectBlocked(ApplicativeGroup group, Fetcher fetcher, Queue<string> nameQueue, RebindTransparent rebindTransparent, Dictionary<string, object> boundVariables)
        //{
        //    Action<string> log = str => Debug.WriteLine($"{str}");
        //    if (group.IsProjectGroup) return new List<Task>
        //        {
        //            new Task(() =>
        //            {
        //                var project = group.Expressions.First();
        //                var rewritten = rebindTransparent.Rewrite(project);
        //                var bindTo = nameQueue.Dequeue();
        //                var wrapped = rewritten.Compile().DynamicInvoke(boundVariables);
        //                var result = Unwrap(wrapped);
        //                log($"Projected '{bindTo}': {result}\n");
        //                boundVariables[bindTo] = result;
        //                rebindTransparent.BlockCount++;
        //            })
        //        };

        //    return .Expressions.Select(exp =>
        //    {
        //        new Task(() =>
        //        {

        //        });
        //    });
        //}


        public static BlockedRequestList GetRequests<A>(SplitBind<A> splits)
        {
            if (!splits.Segments.Any()) return BlockedRequestList.Empty();
            var rebindTransparent = new RebindTransparent();
            var scope = new Scope();
            var list = new List<FetchResult>();

            var segment = splits.Segments.First();
            var rest = splits.Segments.Skip(1);
            if (segment.IsProjectGroup)
            {
                var project = segment.Expressions.First();
                var rewritten = rebindTransparent.Rewrite(project);
                var bindTo = splits.NameQueue.Dequeue();
                Action<Scope> projectResult = boundVars =>
                {
                    var wrapped = rewritten.Compile().DynamicInvoke(boundVars);
                    var result = Unwrap(wrapped);
                    boundVars.Add(bindTo, result);
                };
                rebindTransparent.BlockCount++;
                return new BlockedRequestList(new List<FetchResult> { new ProjectResult(projectResult) }, rest);
            }
            var blocked = segment.Expressions.Select(exp =>
            {
                var bindTo = splits.NameQueue.Dequeue();
                var rewritten = rebindTransparent.Rewrite(exp);
                dynamic request = rewritten.Compile().DynamicInvoke(scope);
                var split = request.Split();
                return (BlockedRequestList) split.CollectRequests(bindTo);
            }).ToList();

            var collected = blocked.Aggregate(BlockedRequestList.Empty(), (acc, br) =>
                new BlockedRequestList(acc.Blocked.Concat(br.Blocked), acc.Remaining.Concat(br.Remaining))
            );
            return collected;
        }

        public static async Task<A> Run<A>(SplitBind<A> splits, Fetcher fetcher, int nestLevel)
        {
            var spacing = String.Empty.PadLeft(nestLevel * 4);
            Action<string> log = str => Debug.WriteLine($"{spacing}{str}");
            var rebindTransparent = new RebindTransparent();
            var scope = new Scope();
            A final = default(A);
            int batchNumber = 1;
            foreach (var segment in splits.Segments)
            {
                if (segment.IsProjectGroup)
                {
                    var project = segment.Expressions.First();
                    var rewritten = rebindTransparent.Rewrite(project);
                    var bindTo = splits.NameQueue.Dequeue();
                    var wrapped = rewritten.Compile().DynamicInvoke(scope);
                    var result = Unwrap(wrapped);
                    log($"Projected '{bindTo}': {result}\n");
                    scope.Add(bindTo, result);
                    rebindTransparent.BlockCount++;
                    continue;
                }
                if (!segment.Expressions.Any()) continue;

                log("");
                log($"=== Batch {nestLevel}.{batchNumber++} ===");

                var compiledFetches = segment.Expressions.Select(async exp =>
                {
                    var bindTo = splits.NameQueue.Dequeue();
                    var rewritten = rebindTransparent.Rewrite(exp);
                    dynamic request = rewritten.Compile().DynamicInvoke(scope);
                    var result = await request.FetchWith(fetcher, nestLevel + 1);
                    scope.Add(bindTo, result);
                    log($"Fetched '{bindTo}': {result}");
                }).ToList();

                await Task.WhenAll(compiledFetches);
                log("");
            }
            var finalProject = rebindTransparent.Rewrite(splits.FinalProject);
            final = (A)finalProject.Compile().DynamicInvoke(scope);

            log($"Result: {final}");
            return final;
        }
    }
}
