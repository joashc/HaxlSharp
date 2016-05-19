using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface Fetcher<A, X>
    {
        X Done(A result);
        X Blocked(Task<Fetch<A>> fetch, IEnumerable<Task> blockedRequests);
        X Bind<B>(Fetch<B> fetch, Func<B, Fetch<A>> bind);
    }

    public class RunFetch<A> : Fetcher<A, Task<A>>
    {
        public Task<A> Bind<B>(Fetch<B> fetch, Func<B, Fetch<A>> bind)
        {
            return bind(fetch.Run(new RunFetch<B>()).Result).Run(this);
        }

        public async Task<A> Blocked(Task<Fetch<A>> fetch, IEnumerable<Task> blockedRequests)
        {
            blockedRequests.All(y => { y.Start(); return true; });
            await Task.WhenAll(blockedRequests);
            var x = await fetch;
            return x.Run(this).Result;
        }

        public Task<A> Done(A result)
        {
            return Task.FromResult(result);
        }
    }
}
