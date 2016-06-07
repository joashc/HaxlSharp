using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using HaxlSharp;
using HaxlSharp.Internal;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class DefaultFetcher : Fetcher
    {
        private readonly Dictionary<Type, Func<BlockedRequest, Response>> _fetchFunctions;
        private readonly Dictionary<Type, Func<BlockedRequest, Task<Response>>> _asyncFetchFunctions;

        public DefaultFetcher(Dictionary<Type, Func<BlockedRequest, Response>> fetchFunctions, Dictionary<Type, Func<BlockedRequest, Task<Response>>> asyncFetchFunctions)
        {
            _fetchFunctions = fetchFunctions;
            _asyncFetchFunctions = asyncFetchFunctions;
        }

        private void ThrowIfUnhandled(BlockedRequest request)
        {
            if (!_fetchFunctions.ContainsKey(request.RequestType) && !_asyncFetchFunctions.ContainsKey(request.RequestType))
                throw new ApplicationException($"No handler for request type '{request.RequestType}' found.");
        }

        public async Task<Response> Fetch(BlockedRequest request)
        {
            ThrowIfUnhandled(request);
            if (_fetchFunctions.ContainsKey(request.RequestType))
            {
                var handler = _fetchFunctions[request.RequestType];
                return await Task.Factory.StartNew(() =>
                {
                    var result = handler(request);
                    request.Resolver.SetResult(result.Value);
                    Debug.WriteLine($"Fetched '{request.BindName}': {result.Value}");
                    return result;
                });
            }
            var asyncHandler = _asyncFetchFunctions[request.RequestType];
            var response = await asyncHandler(request);
            request.Resolver.SetResult(response.Value);
            Debug.WriteLine($"Fetched '{request.BindName}': {response.Value}");
            return response;
        }

        public async Task FetchBatch(IEnumerable<BlockedRequest> requests)
        {
            Debug.WriteLine("==== Batch ====");
            var tasks = requests.Select(Fetch);
            var resultArray = await Task.WhenAll(tasks);
            Debug.WriteLine("");
        }

        public async Task<A> Fetch<A>(Fetch<A> request)
        {
            var cache = new HaxlCache(new HashedRequestKey());
            return await request.FetchWith(this, cache);
        }
    }
}
