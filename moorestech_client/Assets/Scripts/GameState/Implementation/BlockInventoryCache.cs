using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Core.Item.Interface;
using MessagePack;
using UnityEngine;
using Client.Common;
using Client.Game.InGame.Context;

namespace GameState.Implementation
{
    internal class BlockInventoryCache
    {
        private readonly FreshDataCache<Vector3Int, BlockInventoryData> _cache;
        
        public BlockInventoryCache()
        {
            
            // Default values if no configuration provided
            _cache = new FreshDataCache<Vector3Int, BlockInventoryData>(
                expiration: TimeSpan.FromSeconds(30),
                cleanupInterval: TimeSpan.FromMinutes(2),
                maxCacheSize: 100
            );
        }
        
        public async UniTask<IBlockInventory> GetInventoryAsync(Vector3Int position)
        {
            return await _cache.GetOrFetchAsync(position, FetchInventoryFromServer);
        }
        
        public void InvalidateInventory(Vector3Int position)
        {
            _cache.Invalidate(position);
        }
        
        public void InvalidateAll()
        {
            _cache.InvalidateAll();
        }
        
        private async UniTask<BlockInventoryData> FetchInventoryFromServer(Vector3Int position)
        {
            try
            {
                var response = await ClientContext.VanillaApi.Response.GetBlockInventory(position, CancellationToken.None);
                
                if (response == null)
                {
                    Debug.LogWarning($"Failed to fetch inventory for block at {position}");
                    return new BlockInventoryData
                    {
                        Items = new List<IItemStack>(),
                        LastUpdated = DateTime.UtcNow
                    };
                }
                
                return new BlockInventoryData
                {
                    Items = response,
                    LastUpdated = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error fetching inventory for block at {position}: {ex.Message}");
                return new BlockInventoryData
                {
                    Items = new List<IItemStack>(),
                    LastUpdated = DateTime.UtcNow
                };
            }
        }
        
        [MessagePackObject]
        internal class BlockInventoryData : IBlockInventory
        {
            [Key(0)]
            public IReadOnlyList<IItemStack> Items { get; set; }
            
            [Key(1)]
            public DateTime LastUpdated { get; set; }
        }
    }
}