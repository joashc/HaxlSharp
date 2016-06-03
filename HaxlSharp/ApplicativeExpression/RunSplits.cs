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
                var split = fetch.Split(new Splitta<Item>());
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
            var split = fetch.Split(new Splitta<B>());
            var result = await split.Run(new SplitRunner<B>(fetcher, nestLevel + 1));
            return fmap.Compile()(result);
        }
    }

    public class RequestCollector<A> : SplitHandler<A, IEnumerable<FetchResult>>
    {
        public readonly string bindTo;
        public RequestCollector(string bindTo)
        {
            this.bindTo = bindTo;
        }

        public IEnumerable<FetchResult> Bind(SplitBind<A> splits)
        {
            return RunSplits.GetRequests(splits);
        }

        public IEnumerable<FetchResult> Request(Returns<A> request, Type requestType)
        {
            return new List<FetchResult> { new BlockedRequest(request, requestType, bindTo) };
        }

        public IEnumerable<FetchResult> RequestSequence<B, Item>(IEnumerable<B> list, Func<B, Fetch<Item>> bind)
        {
            return list.SelectMany(b =>
            {
                var fetch = bind(b);
                var split = fetch.Split();
                return split.CollectRequests("");
            });
        }

        public IEnumerable<FetchResult> Result(A result)
        {
            return new List<FetchResult> { new ProjectResult(dic => { dic[bindTo] = result; }) };
        }

        public IEnumerable<FetchResult> Select<B>(Fetch<B> fetch, Expression<Func<B, A>> fmap)
        {
            var split = fetch.Split();
            var requests = split.CollectRequests(bindTo);
            var compiled = fmap.Compile();
            return new List<FetchResult> { new ProjectResult(dic => { dic[bindTo] = compiled((B)dic[bindTo]); }) };
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


        public static IEnumerable<BlockedRequest> GetRequests<A>(ApplicativeGroup segment, Queue<string> nameQueue, Dictionary<object, string> boundVariables)
        {
            if (segment.IsProjectGroup) return new List<BlockedRequest>();
            var rebindTransparent = new RebindTransparent();
            return segment.Expressions.SelectMany(exp =>
            {
                var bindTo = nameQueue.Dequeue();
                var rewritten = rebindTransparent.Rewrite(exp);
                dynamic request = rewritten.Compile().DynamicInvoke(boundVariables);
                var split = request.Split();
                return (IEnumerable<BlockedRequest>)split.CollectRequests(bindTo);
            });
        }

        public static IEnumerable<FetchResult> GetRequests<A>(SplitBind<A> splits)
        {
            var rebindTransparent = new RebindTransparent();
            var boundVariables = new Dictionary<string, object>();
            var list = new List<FetchResult>();
            foreach (var segment in splits.Segments)
            {
                if (segment.IsProjectGroup)
                {
                    var project = segment.Expressions.First();
                    var rewritten = rebindTransparent.Rewrite(project);
                    var bindTo = splits.NameQueue.Dequeue();
                    Action<Dictionary<string, object>> projectResult = boundVars =>
                    {
                        var wrapped = rewritten.Compile().DynamicInvoke(boundVars);
                        var result = Unwrap(wrapped);
                        boundVars[bindTo] = result;
                    };
                    list.Add(new ProjectResult(projectResult));
                    rebindTransparent.BlockCount++;
                    continue;
                }
                var blocked = segment.Expressions.SelectMany(exp =>
                {
                    var bindTo = splits.NameQueue.Dequeue();
                    var rewritten = rebindTransparent.Rewrite(exp);
                    dynamic request = rewritten.Compile().DynamicInvoke(boundVariables);
                    var split = request.Split();
                    return (IEnumerable<BlockedRequest>)split.CollectRequests(bindTo);
                });
                list.AddRange(blocked);
            }
            return list;
        }

        public static async Task<A> Run<A>(SplitBind<A> splits, Fetcher fetcher, int nestLevel)
        {
            var spacing = String.Empty.PadLeft(nestLevel * 4);
            Action<string> log = str => Debug.WriteLine($"{spacing}{str}");
            var rebindTransparent = new RebindTransparent();
            var boundVariables = new Dictionary<string, object>();
            A final = default(A);
            int batchNumber = 1;
            foreach (var segment in splits.Segments)
            {
                if (segment.IsProjectGroup)
                {
                    var project = segment.Expressions.First();
                    var rewritten = rebindTransparent.Rewrite(project);
                    var bindTo = splits.NameQueue.Dequeue();
                    var wrapped = rewritten.Compile().DynamicInvoke(boundVariables);
                    var result = Unwrap(wrapped);
                    log($"Projected '{bindTo}': {result}\n");
                    boundVariables[bindTo] = result;
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
                    dynamic request = rewritten.Compile().DynamicInvoke(boundVariables);
                    var result = await request.FetchWith(fetcher, nestLevel + 1);
                    boundVariables[bindTo] = result;
                    log($"Fetched '{bindTo}': {result}");
                }).ToList();

                await Task.WhenAll(compiledFetches);
                log("");
            }
            var finalProject = rebindTransparent.Rewrite(splits.FinalProject);
            final = (A)finalProject.Compile().DynamicInvoke(boundVariables);

            log($"Result: {final}");
            return final;
        }
    }
}
