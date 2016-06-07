using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static HaxlSharp.Internal.Haxl;
using System.Threading.Tasks;

namespace HaxlSharp.Internal
{
    public class HaxlFetch
    {
        public HaxlFetch(Lazy<Result> result)
        {
            Result = result;
        }

        public static HaxlFetch FromFunc(Func<Result> resultFunc)
        {
            return new HaxlFetch(new Lazy<Result>(resultFunc));
        }

        /// <summary>
        /// Applicative pure.
        /// <remarks>
        /// We need a bind variable string because our applicative instance is specialized to (Scope -> Scope) functions.
        /// </remarks>
        /// </summary>
        public static HaxlFetch Pure(string bindTo, object value)
        {
            return FromFunc(() =>
                Done.New(scope => scope.Add(bindTo, value))
            );
        }

        /// <summary>
        /// Identity >>= (scope -> x) = x = x >>= (scope -> Identity)
        /// </summary>
        public static HaxlFetch Identity()
        {
            return FromFunc(() => Done.New(s => s));
        }

        /// <summary>
        /// The result of the fetch.
        /// </summary>
        /// <remarks>
        /// Accessing a Done result before it's fetched is a framework error.
        /// </remarks>
        public readonly Lazy<Result> Result;

        public HaxlFetch Map(Func<Scope, Scope> addResult)
        {
            return new HaxlFetch(new Lazy<Result>(() =>
            {
                var result = Result.Value;
                return result.Match<Result>(
                    done => Done.New(comp(done.AddToScope, addResult)),
                    blocked => Blocked.New(blocked.BlockedRequests, blocked.Continue.Map(addResult))
                );
            }));
        }
    }

    public static class HaxlFetchExt
    {
        /// <summary>
        /// Monad instance for HaxlFetch.
        /// </summary>
        public static HaxlFetch Bind(this HaxlFetch fetch, Func<Scope, HaxlFetch> bind)
        {
            return HaxlFetch.FromFunc(() =>
            {
                var result = fetch.Result.Value;
                return result.Match(
                    done => bind(done.AddToScope(Scope.New())).Result.Value,
                    blocked => Blocked.New(blocked.BlockedRequests, blocked.Continue.Bind(bind))
                );
            });
        }

        /// <summary>
        /// "Applicative" instance for HaxlFetch.
        /// </summary>
        /// <remarks>
        /// This isn't a true applicative instance; we don't have:
        /// 
        /// > (<*>) :: f (a -> b) -> f a -> f b
        /// 
        /// In Haskell Haxl, the applicative instance is used to keep fetched values in scope:
        /// 
        /// > (a, b) <- (,) <$> fetch1 <*> fetch2
        /// 
        /// C# can't do nested lambda scoping, and uses transparent identifers instead.
        /// Because the transparent identifers aren't accessible to us, we use our own scoping system.
        /// 
        /// This means our (a -> b) function is *always* (Scope -> Scope);
        /// we therefore can write our "Applicative" instance as simply a function that takes two Fetches.
        /// </remarks>
        public static HaxlFetch Applicative(this HaxlFetch fetch1, HaxlFetch fetch2)
        {
            return HaxlFetch.FromFunc(() =>
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
