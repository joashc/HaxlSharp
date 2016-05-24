using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface Fetcher<A, X>
    {
        X Done(Func<A> result);
        X Blocked(Fetch<A> fetch, IEnumerable<Task> blockedRequests);
    }

    public class RunFetch<A> : Fetcher<A, Task<A>>
    {
        public async Task<A> Blocked(Fetch<A> fetch, IEnumerable<Task> blockedRequests)
        {
            Debug.WriteLine("==== Batch ====");
            blockedRequests.All(r =>
            {
                r.Start();
                return true;
            });
            await Task.WhenAll(blockedRequests);
            var fetchDone = fetch.Result;
            return await fetchDone.Run(this);
        }

        public Task<A> Done(Func<A> result)
        {
            return Task.Factory.StartNew(result);
        }
    }
}
