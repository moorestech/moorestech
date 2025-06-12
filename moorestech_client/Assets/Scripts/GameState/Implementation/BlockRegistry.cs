using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Network.API;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace GameState.Implementation
{
    public class BlockRegistry : IBlockRegistry, IVanillaApiConnectable, IVanillaApiPollable
    {
        private readonly Dictionary<Vector3Int, ReadOnlyBlock> _blocks = new();

        public BlockRegistry()
        {
        }
        
        public void ConnectToVanillaApi(InitialHandshakeResponse initialHandshakeResponse)
        {
            
            // Initialize blocks from handshake response
            var worldData = initialHandshakeResponse.WorldData;
            foreach (var blockInfo in worldData.Blocks)
            {
                AddOrUpdateBlock(blockInfo.BlockPos, blockInfo.BlockId.AsPrimitive(), ConvertBlockDirection(blockInfo.BlockDirection));
            }
            
            // Apply initial block states
            foreach (var state in initialHandshakeResponse.BlockStates)
            {
                UpdateBlockState(state.Position, state.CurrentStateDetail);
            }
            
            // Subscribe to block events
            SubscribeToBlockEvents();
        }
        
        private void SubscribeToBlockEvents()
        {
            // Block placement event
            ClientContext.VanillaApi.Event.SubscribeEventResponse(PlaceBlockEventPacket.EventTag, payload =>
            {
                var data = MessagePackSerializer.Deserialize<PlaceBlockEventMessagePack>(payload);
                var blockData = data.BlockData;
                AddOrUpdateBlock(blockData.BlockPos, blockData.BlockIdInt, (BlockDirection)blockData.Direction);
            });
            
            // Block removal event
            ClientContext.VanillaApi.Event.SubscribeEventResponse(RemoveBlockToSetEventPacket.EventTag, payload =>
            {
                var data = MessagePackSerializer.Deserialize<RemoveBlockEventMessagePack>(payload);
                RemoveBlock(data.Position);
            });
            
            // Block state change event
            ClientContext.VanillaApi.Event.SubscribeEventResponse(ChangeBlockStateEventPacket.EventTag, payload =>
            {
                var data = MessagePackSerializer.Deserialize<BlockStateMessagePack>(payload);
                UpdateBlockState(data.Position, data.CurrentStateDetail);
            });
        }

        public async UniTask UpdateWithWorldData(WorldDataResponse worldData)
        {
            // Update blocks from world data
            var currentBlockPositions = new HashSet<Vector3Int>(_blocks.Keys);
            var newBlockPositions = new HashSet<Vector3Int>();
            
            foreach (var blockInfo in worldData.Blocks)
            {
                newBlockPositions.Add(blockInfo.BlockPos);
                AddOrUpdateBlock(blockInfo.BlockPos, blockInfo.BlockId.AsPrimitive(), ConvertBlockDirection(blockInfo.BlockDirection));
            }
            
            // Remove blocks that no longer exist
            currentBlockPositions.ExceptWith(newBlockPositions);
            foreach (var removedPos in currentBlockPositions)
            {
                RemoveBlock(removedPos);
            }
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
                block = new ReadOnlyBlock(position, blockId, direction);
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
        
        private BlockDirection ConvertBlockDirection(Game.Block.Interface.BlockDirection direction)
        {
            return direction switch
            {
                Game.Block.Interface.BlockDirection.North => BlockDirection.North,
                Game.Block.Interface.BlockDirection.East => BlockDirection.East,
                Game.Block.Interface.BlockDirection.South => BlockDirection.South,
                Game.Block.Interface.BlockDirection.West => BlockDirection.West,
                _ => BlockDirection.North
            };
        }

        private class ReadOnlyBlock : IReadOnlyBlock
        {
            private readonly Vector3Int _position;
            private int _blockId;
            private BlockDirection _direction;
            private Dictionary<string, byte[]> _stateData = new();

            public int BlockId => _blockId;
            public Vector3Int Position => _position;
            public BlockDirection Direction => _direction;

            public ReadOnlyBlock(Vector3Int position, int blockId, BlockDirection direction)
            {
                _position = position;
                _blockId = blockId;
                _direction = direction;
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
                try
                {
                    var items = await ClientContext.VanillaApi.Response.GetBlockInventory(_position, default);
                    return new BlockInventoryImpl(items, DateTime.UtcNow);
                }
                catch
                {
                    return new BlockInventoryImpl(new List<IItemStack>(), DateTime.UtcNow);
                }
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