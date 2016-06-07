using System.Collections.Generic;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface Fetcher
    {
        Task FetchBatch(IEnumerable<BlockedRequest> requests);

        Task<A> Fetch<A>(Fetch<A> request);
    }
}
