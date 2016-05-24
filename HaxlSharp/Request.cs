using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static HaxlSharp.Haxl;

namespace HaxlSharp
{
    public interface Request<A>
    {
        A RunRequest();
    }

    public interface Unblocker
    {
        void BlockedRequest<A>(Request<A> request, Task<A> fetchTask);
        void BlockedRequests<A, B>(BlockedRequestList<A> br1, BlockedRequestList<B> br2);
    }

    public interface BlockedRequestList<A>
    {
        void Run(Unblocker unblocker);
    }

    public class BlockedRequest<A> : BlockedRequestList<A>
    {
        public readonly Request<A> request;
        public readonly Task<A> fetchTask;
        public BlockedRequest(Request<A> request, Task<A> fetchTask)
        {
            this.request = request;
            this.fetchTask = fetchTask;
        }

        public void Run(Unblocker unblocker)
        {
            unblocker.BlockedRequest(request, fetchTask);
        }
    }

    public class BlockedRequests<B, A> : BlockedRequestList<A>
    {
        public readonly BlockedRequestList<A> br1;
        public readonly BlockedRequestList<B> br2;
        public BlockedRequests(BlockedRequestList<A> br1, BlockedRequestList<B> br2)
        {
            this.br1 = br1;
            this.br2 = br2;
        }

        public void Run(Unblocker unblocker)
        {
            unblocker.BlockedRequests(br1, br2);
        }
    }

    public interface Fetcher
    {
        Task<A> AwaitResult<A>(Request<A> request);
    }

    public static class RequestExt
    {
        public static FetchMonad<A> DataFetch<A>(this Request<A> request, Fetcher fetcher)
        {
            var br = new BlockedRequest<A>(request, fetcher.AwaitResult(request));
            var cont = Fetch(Done(() => br.fetchTask.Result));
            var awaiter = new Task(() => { br.fetchTask.Start(); br.fetchTask.Wait(); });
            return new Blocked<A>(cont, new List<Task> { awaiter });
        }
    }
}
