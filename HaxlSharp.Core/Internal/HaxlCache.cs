using System;
using System.Collections.Generic;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// Caches results per-request for deduplication.
    /// </summary>
    public class HaxlCache
    {
        private readonly CacheKeyGenerator _keyGenerator;
        private readonly Dictionary<string, BlockedRequest> _cache;
        public HaxlCache(CacheKeyGenerator generator)
        {
            _keyGenerator = generator;
            _cache = new Dictionary<string, BlockedRequest>();
        }

        public CacheResult Lookup<A>(Returns<A> request)
        {
            var key = _keyGenerator.ForRequest(request);
            if (!_cache.ContainsKey(key)) return CacheResult.NotFound;
            var blockedRequest = _cache[key];
            return CacheResult.Found(blockedRequest);
        }

        public void Insert<A>(Returns<A> request, BlockedRequest blocked)
        {
            var key = _keyGenerator.ForRequest(request);
            if (_cache.ContainsKey(key)) throw new Exception("Internal Haxl error: attempted to cache duplicate request.");
            _cache[key] = blocked;
        }
    }
}
