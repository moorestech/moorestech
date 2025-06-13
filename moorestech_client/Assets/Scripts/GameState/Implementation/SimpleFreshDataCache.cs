using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameState.Implementation
{
    /// <summary>
    /// A simplified cache implementation that stores data with expiration time.
    /// No background cleanup, no LRU eviction - just simple time-based expiration.
    /// </summary>
    internal class SimpleFreshDataCache<TKey, TData>
    {
        private readonly Dictionary<TKey, CachedEntry> _cache = new();
        private readonly TimeSpan _expiration;
        
        public SimpleFreshDataCache(TimeSpan? expiration = null)
        {
            // Default to 30 seconds if not specified
            _expiration = expiration ?? TimeSpan.FromSeconds(30);
        }
        
        private struct CachedEntry
        {
            public TData Data { get; }
            public DateTime CachedAt { get; }
            
            public CachedEntry(TData data, DateTime cachedAt)
            {
                Data = data;
                CachedAt = cachedAt;
            }
            
            public bool IsFresh(TimeSpan expiration) => DateTime.UtcNow - CachedAt < expiration;
        }
        
        /// <summary>
        /// Gets data from cache if fresh, otherwise fetches using the provided function
        /// </summary>
        public async UniTask<TData> GetOrFetchAsync(TKey key, Func<TKey, UniTask<TData>> fetchFunc)
        {
            if (_cache.TryGetValue(key, out var cached) && cached.IsFresh(_expiration))
            {
                return cached.Data;
            }
            
            // Fetch new data
            var data = await fetchFunc(key);
            
            // Store in cache (overwrites if exists)
            _cache[key] = new CachedEntry(data, DateTime.UtcNow);
            
            return data;
        }
        
        /// <summary>
        /// Tries to get cached data without fetching
        /// </summary>
        public bool TryGetCached(TKey key, out TData data)
        {
            if (_cache.TryGetValue(key, out var cached) && cached.IsFresh(_expiration))
            {
                data = cached.Data;
                return true;
            }
            
            data = default;
            return false;
        }
        
        /// <summary>
        /// Invalidates a specific cache entry
        /// </summary>
        public void Invalidate(TKey key)
        {
            _cache.Remove(key);
        }
        
        /// <summary>
        /// Clears all cache entries
        /// </summary>
        public void InvalidateAll()
        {
            _cache.Clear();
        }
        
        /// <summary>
        /// Gets the current number of entries in the cache (for diagnostics)
        /// </summary>
        public int Count => _cache.Count;
    }
}