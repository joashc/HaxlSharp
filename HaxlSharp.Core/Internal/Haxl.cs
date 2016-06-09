using System;
using System.Linq;
using static HaxlSharp.Internal.Base;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// The Haxl monad.
    /// </summary>
    public class Haxl
    {
        public Haxl(Func<HaxlCache, Result> result)
        {
            Result = result;
        }

        public static Haxl FromFunc(Func<HaxlCache, Result> resultFunc)
        {
            return new Haxl(new Func<HaxlCache, Result>(resultFunc));
        }

        /// <summary>
        /// Applicative pure.
        /// <remarks>
        /// We need a bind variable string because our applicative instance is specialized to (Scope -> Scope) functions.
        /// </remarks>
        /// </summary>
        public static Haxl Pure(string bindTo, object value)
        {
            return FromFunc(cache =>
                Done.New(scope => scope.Add(bindTo, value))
            );
        }

        /// <summary>
        /// Identity >>= (scope -> x) = x = x >>= (scope -> Identity)
        /// </summary>
        public static Haxl Identity()
        {
            return FromFunc(cache => Done.New(s => s));
        }

        /// <summary>
        /// The result of the fetch.
        /// </summary>
        /// <remarks>
        /// Accessing a Done result before it's fetched is a framework error.
        /// </remarks>
        public readonly Func<HaxlCache, Result> Result;

        public Haxl Map(Func<Scope, Scope> addResult)
        {
            return new Haxl(new Func<HaxlCache, Result>(cache =>
            {
                var result = Result(cache);
                return result.Match<Result>(
                    done => Done.New(compose(addResult, done.AddToScope)),
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
        public static Haxl Bind(this Haxl fetch, Func<Scope, Haxl> bind)
        {
            return Haxl.FromFunc(cache =>
            {
                var result = fetch.Result(cache);
                return result.Match(
                    done => bind(done.AddToScope(Scope.New())).Result(cache),
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
        public static Haxl Applicative(this Haxl fetch1, Haxl fetch2)
        {
            return Haxl.FromFunc(cache =>
            {
                var result1 = fetch1.Result(cache);
                var result2 = fetch2.Result(cache);
                return result1.Match
                (
                    done1 => result2.Match<Result>
                    (
                        done2 => Done.New(compose(done2.AddToScope, done1.AddToScope)),
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
