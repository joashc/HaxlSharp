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
                return fetch.FetchWith(fetcher);
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

        public static async Task<A> Run<A>(SplitBind<A> splits, Fetcher fetcher, int nestLevel)
        {
            var spacing = String.Empty.PadLeft(nestLevel * 4);
            Action<string> log = str => Debug.WriteLine($"{spacing}{str}");
            var rebindTransparent = new RebindTransparent();
            var scope = new Scope();
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
                    dynamic fetch = rewritten.Compile().DynamicInvoke(scope);
                    var result = await fetch.FetchWith(fetcher, nestLevel + 1);
                    scope.Add(bindTo, result);
                    log($"Fetched '{bindTo}': {result}");
                }).ToList();

                await Task.WhenAll(compiledFetches);
                log("");
            }
            var final = (A)scope.GetValue("<>HAXL_RESULT");

            log($"Result: {final}");
            return final;
        }
    }
}
