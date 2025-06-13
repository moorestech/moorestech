using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameState.Implementation
{
    internal class FreshDataCache<TKey, TData>
    {
        private readonly Dictionary<TKey, CachedData> _cache = new();
        private readonly TimeSpan _expiration;
        private readonly TimeSpan _cleanupInterval;
        private readonly int _maxCacheSize;
        
        public FreshDataCache(TimeSpan expiration, TimeSpan? cleanupInterval = null, int maxCacheSize = 1000)
        {
            _expiration = expiration;
            _cleanupInterval = cleanupInterval ?? TimeSpan.FromMinutes(5);
            _maxCacheSize = maxCacheSize;
            
            StartCleanupRoutine();
        }
        
        internal class CachedData
        {
            public TData Data { get; set; }
            public DateTime CachedAt { get; set; }
            public DateTime LastAccessedAt { get; set; }
            
            public bool IsFresh(TimeSpan expiration) => DateTime.UtcNow - CachedAt < expiration;
        }
        
        public async UniTask<TData> GetOrFetchAsync(
            TKey key, 
            Func<TKey, UniTask<TData>> fetchFunc)
        {
            if (_cache.TryGetValue(key, out var cached) && cached.IsFresh(_expiration))
            {
                cached.LastAccessedAt = DateTime.UtcNow;
                return cached.Data;
            }
            
            var data = await fetchFunc(key);
            
            EnsureCacheCapacity();
            
            _cache[key] = new CachedData 
            { 
                Data = data, 
                CachedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };
            
            return data;
        }
        
        public bool TryGetCached(TKey key, out TData data)
        {
            if (_cache.TryGetValue(key, out var cached) && cached.IsFresh(_expiration))
            {
                cached.LastAccessedAt = DateTime.UtcNow;
                data = cached.Data;
                return true;
            }
            
            data = default;
            return false;
        }
        
        public void Invalidate(TKey key)
        {
            _cache.Remove(key);
        }
        
        public void InvalidateAll()
        {
            _cache.Clear();
        }
        
        private void EnsureCacheCapacity()
        {
            if (_cache.Count >= _maxCacheSize)
            {
                var toRemove = _cache
                    .OrderBy(kvp => kvp.Value.LastAccessedAt)
                    .Take(_cache.Count / 4)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var key in toRemove)
                {
                    _cache.Remove(key);
                }
            }
        }
        
        private void StartCleanupRoutine()
        {
            CleanupExpiredEntries().Forget();
        }
        
        private async UniTaskVoid CleanupExpiredEntries()
        {
            while (true)
            {
                await UniTask.Delay(_cleanupInterval);
                
                var expiredKeys = _cache
                    .Where(kvp => !kvp.Value.IsFresh(_expiration))
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var key in expiredKeys)
                {
                    _cache.Remove(key);
                }
                
                if (_cache.Count > 0)
                {
                    Debug.Log($"FreshDataCache cleanup: removed {expiredKeys.Count} expired entries, {_cache.Count} remaining");
                }
            }
        }
    }
}