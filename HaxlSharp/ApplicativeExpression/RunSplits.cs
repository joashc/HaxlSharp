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
        public SplitRunner(Fetcher fetcher)
        {
            this.fetcher = fetcher;
        }

        public Task<A> Bind(SplitBind<A> splits)
        {
            return RunSplits.Run(splits, fetcher);
        }

        public async Task<A> Request(Returns<A> request, Type requestType)
        {
            var result = await fetcher.Fetch(new GenericRequest(request, requestType, ""));
            return (A)result.Value;
        }

        public async Task<A> RequestSequence<B, Item>(IEnumerable<B> list, Func<B, Fetch<Item>> bind)
        {
            var tasks = list.Select(b =>
            {
                var fetch = bind(b);
                var split = fetch.Split(new Splitta<Item>());
                return split.Run(new SplitRunner<Item>(fetcher));
            });
            var results = await Task.WhenAll(tasks);
            return (A) (object) results.ToList();
        }

        public Task<A> Result(A result)
        {
            return Task.FromResult(result);
        }

        public async Task<A> Select<B>(Fetch<B> fetch, Expression<Func<B, A>> fmap)
        {
            var split = fetch.Split(new Splitta<B>());
            var result = await split.Run(new SplitRunner<B>(fetcher));
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

        private static GenericRequest CreateGenericRequest(object fetch, string bindName)
        {
            var type = fetch.GetType();
            if (IsGenericTypeOf(type, typeof(Request<>)))
            {
                var fetchReq = (dynamic)fetch;
                var requestType = fetchReq.RequestType;
                return new GenericRequest(fetchReq.request, requestType, bindName);
            }
            throw new ArgumentException($"Can't create request from Fetch type {fetch.GetType()}");
        }

        private static void StoreResults(IEnumerable<Result> results, Dictionary<string, object> store)
        {
            foreach (var result in results)
            {
                if (store.ContainsKey(result.BindName)) throw new ApplicationException($"Bind variable {result.BindName} has already been written.");
                store[result.BindName] = result.Value;
                Debug.WriteLine($"Fetched '{result.BindName}': {result.Value}");
            }
        }

        public static async Task<A> Run<A>(SplitBind<A> splits, Fetcher fetcher)
        {
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
                    Debug.WriteLine($"Projected '{bindTo}': {result}\n");
                    boundVariables[bindTo] = result;
                    rebindTransparent.BlockCount++;
                    continue;
                }
                if (!segment.Expressions.Any()) continue;

                Debug.WriteLine($"=== Batch {batchNumber++} ===");

                var compiledFetches = segment.Expressions.Select(async exp =>
                {
                    var bindTo = splits.NameQueue.Dequeue();
                    var rewritten = rebindTransparent.Rewrite(exp);
                    dynamic request =  rewritten.Compile().DynamicInvoke(boundVariables);
                    var result = await request.FetchWith(fetcher);
                    boundVariables[bindTo] = result;
                    Debug.WriteLine($"Fetched '{bindTo}': {result}");
                }).ToList();

                await Task.WhenAll(compiledFetches);
                Debug.WriteLine("");
            }
            var finalProject = rebindTransparent.Rewrite(splits.FinalProject);
            final = (A)finalProject.Compile().DynamicInvoke(boundVariables);

            Debug.WriteLine($"Result: {final}");
            return final;
        }
    }
}
