using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// The result of a fetch.
    /// </summary>
    public interface Result
    {
        X Match<X>(Func<Done, X> done, Func<Blocked, X> blocked);
    }

    /// <summary>
    /// The result of a completed fetch.
    /// </summary>
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

    /// <summary>
    /// A result that's blocked on one or more requests.  
    /// </summary>
    public class Blocked : Result
    {
        public readonly IEnumerable<BlockedRequest> BlockedRequests;
        public readonly Haxl Continue;

        public static Blocked New(IEnumerable<BlockedRequest> blocked, Haxl cont)
        {
            return new Blocked(blocked, cont);
        }

        private Blocked(IEnumerable<BlockedRequest> blocked, Haxl cont)
        {
            BlockedRequests = blocked;
            Continue = cont;
        }

        public X Match<X>(Func<Done, X> done, Func<Blocked, X> blocked)
        {
            return blocked(this);
        }
    }
}
