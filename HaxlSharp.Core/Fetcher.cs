using System.Collections.Generic;
using System.Threading.Tasks;

namespace HaxlSharp
{
    /// <summary>
    /// Fetches a request.
    /// </summary>
    public interface Fetcher
    {
        Task FetchBatch(IEnumerable<BlockedRequest> requests);

        Task<A> Fetch<A>(Fetch<A> request);
    }
}
