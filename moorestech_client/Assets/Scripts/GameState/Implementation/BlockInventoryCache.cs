using System;
using System.Collections.Generic;
using System.Threading;
using Client.Common;
using Client.Network.API;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using MessagePack;
using UnityEngine;

namespace GameState.Implementation
{
    internal class BlockInventoryCache
    {
        private readonly SimpleFreshDataCache<Vector3Int, BlockInventoryData> _cache;
        private readonly VanillaApi _vanillaApi;
        
        public BlockInventoryCache(VanillaApi vanillaApi)
        {
            _vanillaApi = vanillaApi;
            // Simple cache with 30 second expiration
            _cache = new SimpleFreshDataCache<Vector3Int, BlockInventoryData>(TimeSpan.FromSeconds(30));
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
                var response = await _vanillaApi.Response.GetBlockInventory(position, CancellationToken.None);
                
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
        
        internal class BlockInventoryData : IBlockInventory
        {
            public IReadOnlyList<IItemStack> Items { get; set; }
            
            public DateTime LastUpdated { get; set; }
        }
    }
}