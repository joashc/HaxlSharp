using System;
using System.Collections.Generic;
using System.Linq;
using static HaxlSharp.Haxl;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface Result<A> : Fetch<A>
    {
        X Run<X>(Fetcher<A, X> fetcher);
    }

    public class Done<A> : Result<A>
    {
        public readonly Func<A> result;
        public Dictionary<string, object> PreviouslyBound { get { return new Dictionary<string, object>(); } }
        public Done(Func<A> result)
        {
            this.result = result;
        }

        public X Run<X>(FetchRewriter<A, X> rewriter)
        {
            return rewriter.Result(this);
        }

        public X Run<X>(Fetcher<A, X> fetcher)
        {
            return fetcher.Done(result);
        }
    }

    public class Blocked<A> : Result<A>
    {
        public readonly Result<A> result;
        public readonly IEnumerable<Task> blockedRequests;
        public Dictionary<string, object> PreviouslyBound { get { return new Dictionary<string, object>(); } }
        public Blocked(Result<A> fetch, IEnumerable<Task> blockedRequests)
        {
            this.result = fetch;
            this.blockedRequests = blockedRequests;
        }

        public X Run<X>(Fetcher<A, X> fetcher)
        {
            return fetcher.Blocked(result, blockedRequests);
        }

        public X Run<X>(FetchRewriter<A, X> rewriter)
        {
            return rewriter.Result(this);
        }
    }

    public static class ResultExt
    {
        public static Result<B> Select<A, B>(this Result<A> result, Func<A, B> f)
        {
            if (result is Done<A>)
            {
                var doneA = result as Done<A>;
                return Done(() => f(doneA.result()));
            }
            if (result is Blocked<A>)
            {
                var blockedA = result as Blocked<A>;
                return Blocked(blockedA.result.Select(f), blockedA.blockedRequests);
            }
            throw new ArgumentException();
        }

        public static Result<B> SelectMany<A, B>(this Result<A> result, Func<A, Result<B>> bind)
        {
            if (result is Done<A>)
            {
                var doneA = result as Done<A>;
                return bind(doneA.result());
            }
            if (result is Blocked<A>)
            {
                var blockedA = result as Blocked<A>;
                var newFetch = blockedA.result.SelectMany(bind);
                return Blocked(newFetch, blockedA.blockedRequests);
            }
            throw new ArgumentException();
        }

        public static Result<C> SelectMany<A, B, C>(this Result<A> fetch, Func<A, Result<B>> bind, Func<A, B, C> project)
        {
            return fetch.SelectMany(t => bind(t).Select(u => project(t, u)));
        }

        public static Task<A> RunFetch<A>(this Result<A> fetch)
        {
            return fetch.Run(new RunFetch<A>());
        }

    }
}
