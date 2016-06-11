using System;
using static HaxlSharp.Internal.Base;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HaxlSharp.Internal;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class HaxlFetcher : Fetcher
    {
        private readonly Dictionary<Type, Func<BlockedRequest, Response>> _fetchFunctions;
        private readonly Dictionary<Type, Func<BlockedRequest, Task<Response>>> _asyncFetchFunctions;
        bool isLogging;

        public HaxlFetcher(Dictionary<Type, Func<BlockedRequest, Response>> fetchFunctions, Dictionary<Type, Func<BlockedRequest, Task<Response>>> asyncFetchFunctions, Action<HaxlLogEntry> onLogEntry = null)
        {
            _fetchFunctions = fetchFunctions;
            _asyncFetchFunctions = asyncFetchFunctions;
            isLogging = onLogEntry != null;
            if (isLogging) OnLogEntry += log => onLogEntry(log);
            else OnLogEntry += log => { };
        }

        private void ThrowIfUnhandled(BlockedRequest request)
        {
            if (!_fetchFunctions.ContainsKey(request.RequestType) && !_asyncFetchFunctions.ContainsKey(request.RequestType))
            {
                RaiseLogEntry(Error($"No handler for request type '{request.RequestType}' found."));
                throw new ApplicationException($"No handler for request type '{request.RequestType}' found.");
            }
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
                    RaiseLogEntry(Info($"Fetched '{request.BindName}': {result.Value}"));
                    return result;
                });
            }
            var asyncHandler = _asyncFetchFunctions[request.RequestType];
            var response = await asyncHandler(request);
            request.Resolver.SetResult(response.Value);
            RaiseLogEntry(Info($"Fetched '{request.BindName}': {response.Value}"));
            return response;
        }

        public async Task FetchBatch(IEnumerable<BlockedRequest> requests)
        {
            RaiseLogEntry(Info("==== Batch ===="));
            var tasks = requests.Select(Fetch);
            await Task.WhenAll(tasks);
        }

        public delegate void HandleLogEntry(HaxlLogEntry logEntry);

        public event HandleLogEntry OnLogEntry;

        private void RaiseLogEntry(HaxlLogEntry logEntry)
        {
            if (!isLogging) return;
            OnLogEntry(logEntry);
        }

        public async Task<A> Fetch<A>(Fetch<A> request)
        {
            var cache = new HaxlCache(new HashedRequestKey());
            return await request.FetchWith(this, cache, RaiseLogEntry);
        }
    }
}
