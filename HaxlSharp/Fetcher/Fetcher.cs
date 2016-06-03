using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface Fetcher
    {
        Task<Result> Fetch(BlockedRequest request);
        Task<IEnumerable<Result>> FetchBatch(IEnumerable<BlockedRequest> requests);

        Task<A> Fetch<A>(Fetch<A> request, int nestLevel);
    }

    public class DefaultFetcher : Fetcher
    {
        private readonly Dictionary<Type, Func<BlockedRequest, Result>> _fetchFunctions;
        private readonly Dictionary<Type, Func<BlockedRequest, Task<Result>>> _asyncFetchFunctions;

        public DefaultFetcher(Dictionary<Type, Func<BlockedRequest, Result>> fetchFunctions, Dictionary<Type, Func<BlockedRequest, Task<Result>>> asyncFetchFunctions)
        {
            _fetchFunctions = fetchFunctions;
            _asyncFetchFunctions = asyncFetchFunctions;
        }

        private void ThrowIfUnhandled(BlockedRequest request)
        {
            if (!_fetchFunctions.ContainsKey(request.RequestType) && !_asyncFetchFunctions.ContainsKey(request.RequestType))
                throw new ApplicationException($"No handler for request type '{request.RequestType}' found.");
        }

        public async Task<Result> Fetch(BlockedRequest request)
        {
            ThrowIfUnhandled(request);
            if (_fetchFunctions.ContainsKey(request.RequestType))
            {
                var handler = _fetchFunctions[request.RequestType];
                return await Task.Factory.StartNew(() => handler(request));
            }
            var asyncHandler = _asyncFetchFunctions[request.RequestType];
            return await asyncHandler(request);
        }

        public async Task<IEnumerable<Result>> FetchBatch(IEnumerable<BlockedRequest> requests)
        {
            var tasks = requests.Select(Fetch);
            var resultArray = await Task.WhenAll(tasks);
            return resultArray;
        }

        public async Task<A> Fetch<A>(Fetch<A> request, int nestLevel)
        {
            return await request.FetchWith(this, nestLevel);
        }
    }
}
