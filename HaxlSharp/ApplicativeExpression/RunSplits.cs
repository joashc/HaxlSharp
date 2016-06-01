using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class CompiledFetch
    {
        public readonly string BindName;
        public readonly object Fetch;
        public bool IsSequence
        {
            get
            {
                var type = Fetch.GetType();
                return RunSplits.IsGenericTypeOf(type, typeof(RequestSequence<,>));
            }
        }
        public bool IsValue
        {
            get
            {
                var type = Fetch.GetType();
                return RunSplits.IsGenericTypeOf(type, typeof(FetchResult<>));
            }
        }
        public CompiledFetch(object fetch, string bindTo)
        {
            BindName = bindTo;
            Fetch = fetch;
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
                return ((dynamic)result).Val;
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
                Debug.WriteLine($"Fetched {result.BindName}: {result.Value}");
            }
        }

        public static async Task<A> Run<A>(SplitApplicatives<A> splits, Fetcher fetcher)
        {
            if (splits.Expression is FetchResult<A>)
            {
                return await Task.FromResult((splits.Expression as FetchResult<A>).Val);
            }
            if (splits.Expression is Request<A>)
            {
                var request = splits.Expression as Request<A>;
                var result = await fetcher.Fetch(new GenericRequest(request.request, request.request.GetType(), ""));
                return (A)result.Value;
            }

            var rebindTransparent = new RebindTransparent();
            var boundVariables = new Dictionary<string, object>();
            A final = default(A);
            int i = 1;
            foreach (var segment in splits.Segments)
            {
                if (segment.IsProjectGroup)
                {
                    var project = segment.Expressions.First();
                    var rewritten = rebindTransparent.Rewrite(project);
                    var bindTo = splits.NameQueue.Dequeue();
                    var wrapped = rewritten.Compile().DynamicInvoke(boundVariables);
                    var result = Unwrap(wrapped);
                    Debug.WriteLine($"Projected {bindTo}: {result}\n");
                    boundVariables[bindTo] = result;
                    continue;
                }
                if (!segment.Expressions.Any()) continue;

                Debug.WriteLine($"=== Batch {i++} ===");
                var compiledFetches = segment.Expressions.Select(exp =>
                {
                    var bindTo = splits.NameQueue.Dequeue();
                    var rewritten = rebindTransparent.Rewrite(exp);
                    var request = rewritten.Compile().DynamicInvoke(boundVariables);
                    return new CompiledFetch(request, bindTo);
                }).ToList();

                var segmentRequests = compiledFetches
                    .Where(c => !c.IsSequence && !c.IsValue)
                    .Select(c => CreateGenericRequest(c.Fetch, c.BindName));

                var sequenceRequests = compiledFetches.Where(c => c.IsSequence).Select(c =>
                {
                    return Task.Factory.StartNew(() =>
                    {
                        dynamic sequence = c.Fetch;
                        var result = sequence.FetchMe(fetcher);
                        boundVariables[c.BindName] = result;
                        Debug.WriteLine($"Fetched {c.BindName}: {result}");
                    });
                }).ToList();

                var resulted = compiledFetches.Where(c => c.IsValue);
                foreach (var result in resulted)
                {
                    var value = ((dynamic)result.Fetch).Val;
                    boundVariables[result.BindName] = value;
                }


                var results = await fetcher.FetchBatch(segmentRequests);
                await Task.WhenAll(sequenceRequests);
                StoreResults(results, boundVariables);
                Debug.WriteLine("");
            }
            var finalProject = rebindTransparent.Rewrite(splits.FinalProject);
            final = (A)finalProject.Compile().DynamicInvoke(boundVariables);

            Debug.WriteLine($"Result: {final}");
            return final;
        }
    }
}
