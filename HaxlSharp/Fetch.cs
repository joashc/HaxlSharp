using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static HaxlSharp.Haxl;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class Fetch
    {
        public Fetch(Lazy<Result> result)
        {
            Result = result;
        }

        public static Fetch FromFunc(Func<Result> resultFunc)
        {
            return new Fetch(new Lazy<Result>(resultFunc));
        }

        public readonly Lazy<Result> Result;

        public Fetch Map(Func<Scope, Scope> addResult)
        {
            return new Fetch(new Lazy<Result>(() =>
            {
                var result = Result.Value;
                return result.Match<Result>(
                    done => Done.New(comp(done.AddToScope, addResult)),
                    blocked => Blocked.New(blocked.BlockedRequests, blocked.Continue.Map(addResult))
                );
            }));
        }
    }

    public interface Result
    {
        X Match<X>(Func<Done, X> done, Func<Blocked, X> blocked);
    }

    public class Done : Result
    {
        public Func<Scope, Scope> AddToScope;

        public static Done New(Func<Scope, Scope> addToScope)
        {
            return new Done(addToScope);
        }

        public Done(Func<Scope, Scope> addToScope)
        {
            AddToScope = addToScope;
        }

        public X Match<X>(Func<Done, X> done, Func<Blocked, X> blocked)
        {
            return done(this);
        }
    }

    public class Blocked : Result
    {
        public readonly IEnumerable<BlockedRequest> BlockedRequests;
        public readonly Fetch Continue;

        public static Blocked New(IEnumerable<BlockedRequest> blocked, Fetch cont)
        {
            return new Blocked(blocked, cont);
        }

        private Blocked(IEnumerable<BlockedRequest> blocked, Fetch cont)
        {
            BlockedRequests = blocked;
            Continue = cont;
        }

        public X Match<X>(Func<Done, X> done, Func<Blocked, X> blocked)
        {
            return blocked(this);
        }
    }

    public static class FetchExt
    {
        public static Fetch Bind(this Fetch fetch, Func<Scope, Fetch> bind)
        {
            return Fetch.FromFunc(() =>
            {
                var result = fetch.Result.Value;
                return result.Match(
                    done => bind(done.AddToScope(Scope.New())).Result.Value,
                    blocked => Blocked.New(blocked.BlockedRequests, blocked.Continue.Bind(bind))
                );
            });
        }

        public static Fetch Applicative(this Fetch fetch1, Fetch fetch2)
        {
            return Fetch.FromFunc(() =>
            {
                var result1 = fetch1.Result.Value;
                var result2 = fetch2.Result.Value;
                return result1.Match
                (
                    done1 => result2.Match<Result>
                    (
                        done2 => Done.New(comp(done1.AddToScope, done2.AddToScope)),
                        blocked2 => Blocked.New(blocked2.BlockedRequests, blocked2.Continue.Map(done1.AddToScope))
                    ),

                    blocked1 => result2.Match<Result>
                    (
                        done2 => Blocked.New(blocked1.BlockedRequests, blocked1.Continue.Map(done2.AddToScope)),
                        blocked2 => Blocked.New(
                            blocked1.BlockedRequests.Concat(blocked2.BlockedRequests),
                            blocked1.Continue.Applicative(blocked2.Continue)
                        )
                    )
                );
            });
        }
    }


}
