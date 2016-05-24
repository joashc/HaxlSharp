using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface Fetcher<C, X>
    {
        X Done(Func<C> result);
        X Blocked(Fetch<C> fetch, IEnumerable<Task> blockedRequests);
    }

    public class RunFetch<C> : Fetcher<C, Task<C>>
    {
        public async Task<C> Blocked(Fetch<C> fetch, IEnumerable<Task> blockedRequests)
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

        public Task<C> Done(Func<C> result)
        {
            return Task.Factory.StartNew(result);
        }

    }
}
