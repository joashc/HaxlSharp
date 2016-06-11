using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HaxlSharp.Internal
{
    public static class RunFetch
    {
        /// <summary>
        /// Repeatedly fetches requests until we have the result.
        /// </summary>
        public static async Task<Scope> Run(Haxl fetch, Scope scope, Func<IEnumerable<BlockedRequest>, Task> fetcher, HaxlCache cache, Action<HaxlLogEntry> logger)
        {
            var result = fetch.Result(cache, logger);
            return await result.Match(
                done => Task.FromResult(done.AddToScope(scope)),
                async blocked =>
                {
                    await fetcher(blocked.BlockedRequests);
                    return await Run(blocked.Continue, scope, fetcher, cache, logger);
                }
            );
        }
    }
}
