using System;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// The result of a cache lookup. 
    /// </summary>
    public abstract class CacheResult
    {
        public abstract X Match<X>(Func<Unit, X> notFound, Func<BlockedRequest, X> found);
        public static NotFound NotFound = new NotFound();
        public static Found Found(BlockedRequest request)
        {
            return new Found(request);
        }
    }

    /// <summary>
    /// An item matching the cache key was not found.
    /// </summary>
    public class NotFound : CacheResult
    {
        public override X Match<X>(Func<Unit, X> notFound, Func<BlockedRequest, X> found)
        {
            return notFound(Base.Unit);
        }
    }

    /// <summary>
    /// The item matching the cache key.
    /// </summary>
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
