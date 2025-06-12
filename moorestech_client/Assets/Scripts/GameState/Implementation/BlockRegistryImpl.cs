using System;
using System.Collections.Generic;
using System.Threading;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using MessagePack;
using UnityEngine;

namespace GameState.Implementation
{
    public class BlockRegistryImpl : IBlockRegistry
    {
        private readonly Dictionary<Vector3Int, ReadOnlyBlock> _blocks = new();
        private Func<Vector3Int, CancellationToken, UniTask<List<IItemStack>>> _inventoryFetcher;

        public BlockRegistryImpl()
        {
        }

        public void SetInventoryFetcher(Func<Vector3Int, CancellationToken, UniTask<List<IItemStack>>> fetcher)
        {
            _inventoryFetcher = fetcher;
        }

        public IReadOnlyBlock GetBlock(Vector3Int position)
        {
            return _blocks.TryGetValue(position, out var block) ? block : null;
        }

        public IReadOnlyDictionary<Vector3Int, IReadOnlyBlock> AllBlocks
        {
            get
            {
                var result = new Dictionary<Vector3Int, IReadOnlyBlock>();
                foreach (var kvp in _blocks)
                {
                    result[kvp.Key] = kvp.Value;
                }
                return result;
            }
        }

        public void AddOrUpdateBlock(Vector3Int position, int blockId, BlockDirection direction)
        {
            if (!_blocks.TryGetValue(position, out var block))
            {
                block = new ReadOnlyBlock(position, blockId, direction, _inventoryFetcher);
                _blocks[position] = block;
            }
            else
            {
                block.UpdateBlockInfo(blockId, direction);
            }
        }

        public void RemoveBlock(Vector3Int position)
        {
            _blocks.Remove(position);
        }

        public void UpdateBlockState(Vector3Int position, Dictionary<string, byte[]> stateData)
        {
            if (_blocks.TryGetValue(position, out var block))
            {
                block.UpdateStateData(stateData);
            }
        }

        private class ReadOnlyBlock : IReadOnlyBlock
        {
            private readonly Vector3Int _position;
            private int _blockId;
            private BlockDirection _direction;
            private Dictionary<string, byte[]> _stateData = new();
            private readonly Func<Vector3Int, CancellationToken, UniTask<List<IItemStack>>> _inventoryFetcher;

            public int BlockId => _blockId;
            public Vector3Int Position => _position;
            public BlockDirection Direction => _direction;

            public ReadOnlyBlock(Vector3Int position, int blockId, BlockDirection direction, 
                Func<Vector3Int, CancellationToken, UniTask<List<IItemStack>>> inventoryFetcher)
            {
                _position = position;
                _blockId = blockId;
                _direction = direction;
                _inventoryFetcher = inventoryFetcher;
            }

            public void UpdateBlockInfo(int blockId, BlockDirection direction)
            {
                _blockId = blockId;
                _direction = direction;
            }
            
            public void UpdateStateData(Dictionary<string, byte[]> stateData)
            {
                _stateData = stateData;
            }

            public T GetState<T>(string stateKey) where T : class
            {
                if (_stateData.TryGetValue(stateKey, out var bytes))
                {
                    try
                    {
                        return MessagePackSerializer.Deserialize<T>(bytes) as T;
                    }
                    catch
                    {
                        return null;
                    }
                }
                return null;
            }

            public async UniTask<IBlockInventory> GetInventoryAsync()
            {
                if (_inventoryFetcher == null)
                {
                    return new BlockInventoryImpl(new List<IItemStack>(), DateTime.UtcNow);
                }
                
                var items = await _inventoryFetcher(_position, default);
                return new BlockInventoryImpl(items, DateTime.UtcNow);
            }
        }

        private class BlockInventoryImpl : IBlockInventory
        {
            public IReadOnlyList<IItemStack> Items { get; }
            public DateTime LastUpdated { get; }

            public BlockInventoryImpl(IReadOnlyList<IItemStack> items, DateTime lastUpdated)
            {
                Items = items;
                LastUpdated = lastUpdated;
            }
        }
    }

    public class CommonMachineState
    {
        public float CurrentPower { get; set; }
        public float RequestPower { get; set; }
        public float ProcessingRate { get; set; }
        public string CurrentStateType { get; set; }
    }
}