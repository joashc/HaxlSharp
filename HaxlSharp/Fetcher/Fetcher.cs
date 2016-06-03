using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface Fetcher
    {
        Task<Result> Fetch(GenericRequest request);
        Task<IEnumerable<Result>> FetchBatch(IEnumerable<GenericRequest> requests);

        Task<A> Fetch<A>(Fetch<A> request);
    }

    public class DefaultFetcher : Fetcher
    {
        private readonly Dictionary<Type, Func<GenericRequest, Result>> _fetchFunctions;
        private readonly Dictionary<Type, Func<GenericRequest, Task<Result>>> _asyncFetchFunctions;

        public DefaultFetcher(Dictionary<Type, Func<GenericRequest, Result>> fetchFunctions, Dictionary<Type, Func<GenericRequest, Task<Result>>> asyncFetchFunctions)
        {
            _fetchFunctions = fetchFunctions;
            _asyncFetchFunctions = asyncFetchFunctions;
        }

        private void ThrowIfUnhandled(GenericRequest request)
        {
            if (!_fetchFunctions.ContainsKey(request.RequestType) && !_asyncFetchFunctions.ContainsKey(request.RequestType))
                throw new ApplicationException($"No handler for request type '{request.RequestType}' found.");
        }

        public async Task<Result> Fetch(GenericRequest request)
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

        public async Task<IEnumerable<Result>> FetchBatch(IEnumerable<GenericRequest> requests)
        {
            var tasks = requests.Select(Fetch);
            var resultArray = await Task.WhenAll(tasks);
            return resultArray;
        }

        public Task<A> Fetch<A>(Fetch<A> request)
        {
            return request.FetchWith(this);
        }
    }
}
