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
        X Bind<B>(Result<B> hf, Expression<Func<B, Result<C>>> bind);
        X Applicative<A, B>(Result<A> hf, Func<Result<B>> applicative, Func<A, B, C> project);
    }

    public class RunFetch<C> : Fetcher<C, Task<C>>
    {
        public Task<C> Applicative<A, B>(Result<A> hf, Func<Result<B>> applicative, Func<A, B, C> project)
        {
            throw new NotImplementedException();
        }

        public Task<C> Bind<B>(Result<B> hf, Expression<Func<B, Result<C>>> bind)
        {
            throw new NotImplementedException();
        }

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
