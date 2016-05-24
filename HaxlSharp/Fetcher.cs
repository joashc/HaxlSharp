using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    /// <summary>
    /// Fetches result
    /// </summary>
    public interface Fetcher<C, X>
    {
        X Done(Func<C> result);
        X Blocked(Result<C> result, IEnumerable<Task> blockedRequests);
    }

    public static class BatchEvents
    {
        public delegate void BatchOccurredHandler(int batchSize, Type resultType);
        public static event BatchOccurredHandler BatchOccurredEvent;
        public static void BatchOccurred(int batchSize, Type resultType)
        {
            if (BatchOccurredEvent == null) return;
            BatchOccurredEvent(batchSize, resultType);
        }
    }

    /// <summary>
    /// Fetcher that prints a separator between concurrent requests.
    /// </summary>
    /// <typeparam name="C"></typeparam>
    public class RunFetch<C> : Fetcher<C, Task<C>>
    {
        public async Task<C> Blocked(Result<C> fetch, IEnumerable<Task> blockedRequests)
        {
            Debug.WriteLine("==== Batch ====");
            BatchEvents.BatchOccurred(blockedRequests.Count(), typeof(C));
            foreach (var blocked in blockedRequests)
            {
                blocked.Start();
            }
            await Task.WhenAll(blockedRequests);
            return await fetch.Run(this);
        }

        public Task<C> Done(Func<C> result)
        {
            return Task.Factory.StartNew(result);
        }
    }
}
