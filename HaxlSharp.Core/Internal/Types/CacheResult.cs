using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp.Internal
{
    public abstract class CacheResult
    {
        public abstract X Match<X>(Func<Unit, X> notFound, Func<BlockedRequest, X> found);
        public static NotFound NotFound = new NotFound();
        public static Found Found(BlockedRequest request)
        {
            return new Found(request);
        }
    }

    public class NotFound : CacheResult
    {
        public override X Match<X>(Func<Unit, X> notFound, Func<BlockedRequest, X> found)
        {
            return notFound(Base.Unit);
        }
    }

    public class Found : CacheResult
    {
        private readonly BlockedRequest _blocked;
        public Found(BlockedRequest blocked)
        {
            _blocked = blocked;
        }

        public override X Match<X>(Func<Unit, X> notFound, Func<BlockedRequest, X> found)
        {
            return found(_blocked);
        }
    }
}
