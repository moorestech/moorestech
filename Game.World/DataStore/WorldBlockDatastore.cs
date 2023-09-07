using System;
using System.Collections.Generic;
using Core.Block.BlockFactory;
using Core.Block.Blocks;
using Core.Block.Blocks.State;
using Core.Const;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using World.Event;

namespace World.DataStore
{
    /// <summary>
    /// ワールドに存在するブロックとその座標の対応づけを行います。
    /// </summary>
    public class WorldBlockDatastore : IWorldBlockDatastore
    {
        //メインのデータストア
        private readonly Dictionary<int, WorldBlockData> _blockMasterDictionary = new();

        //座標とキーの紐づけ
        private readonly Dictionary<Coordinate, int> _coordinateDictionary = new();

        public event Action<(ChangedBlockState state, IBlock block, int x, int y)> OnBlockStateChange;


        readonly IBlock _nullBlock = new NullBlock();
        private readonly BlockPlaceEvent _blockPlaceEvent;
        private readonly BlockRemoveEvent _blockRemoveEvent;
        private readonly BlockFactory _blockFactory;

        public WorldBlockDatastore(IBlockPlaceEvent blockPlaceEvent, BlockFactory blockFactory,
            IBlockRemoveEvent blockRemoveEvent)
        {
            _blockFactory = blockFactory;
            _blockRemoveEvent = (BlockRemoveEvent) blockRemoveEvent;
            _blockPlaceEvent = (BlockPlaceEvent) blockPlaceEvent;
        }

        public bool AddBlock(IBlock block, int x, int y, BlockDirection blockDirection)
        {
            //既にキーが登録されてないか、同じ座標にブロックを置こうとしてないかをチェック
            if (!_blockMasterDictionary.ContainsKey(block.EntityId) &&
                !_coordinateDictionary.ContainsKey(new Coordinate(x, y)))
            {
                var c = new Coordinate(x, y);
                var data = new WorldBlockData(block, x, y, blockDirection);
                _blockMasterDictionary.Add(block.EntityId, data);
                _coordinateDictionary.Add(c, block.EntityId);
                _blockPlaceEvent.OnBlockPlaceEventInvoke(new BlockPlaceEventProperties(c, data.Block, blockDirection));

                block.OnBlockStateChange += state =>
                {
                    OnBlockStateChange?.Invoke((state, block, x, y));
                };

                return true;
            }

            return false;
        }

        public bool RemoveBlock(int x, int y)
        {
            if (!Exists(x, y)) return false;
            
            var entityId = GetEntityId(x, y);
            if (!_blockMasterDictionary.ContainsKey(entityId)) return false;

            var data = _blockMasterDictionary[entityId];

            _blockRemoveEvent.OnBlockRemoveEventInvoke(new BlockRemoveEventProperties(
                new Coordinate(x, y), data.Block));

            _blockMasterDictionary.Remove(entityId);
            _coordinateDictionary.Remove(new Coordinate(x, y));
            return true;
        }


        public IBlock GetBlock(int x, int y)
        {
            var c = new Coordinate(x, y);
            if (_coordinateDictionary.ContainsKey(c)) return _blockMasterDictionary[_coordinateDictionary[c]].Block;
            return _nullBlock;
        }

        public bool TryGetBlock(int x, int y, out IBlock block)
        {
            if (Exists(x,y))
            {
                block = GetBlock(x, y);
                return true;
            }
            block = _nullBlock;
            return false;
        }

        public (int, int) GetBlockPosition(int entityId)
        {
            if (_blockMasterDictionary.ContainsKey(entityId))
            {
                var data = _blockMasterDictionary[entityId];
                return (data.X, data.Y);
            }

            throw new Exception("ブロックがありません");
        }

        public BlockDirection GetBlockDirection(int x, int y)
        {
            var c = new Coordinate(x, y);
            if (_coordinateDictionary.ContainsKey(c))
                return _blockMasterDictionary[_coordinateDictionary[c]].BlockDirection;
            return BlockDirection.North;
        }
        
        
        public bool ExistsComponentBlock<TComponent>(int x, int y)
        {
            return GetBlock(x, y) is TComponent;
        }

        public TComponent GetBlock<TComponent>(int x, int y)
        {
            var block = GetBlock(x, y);
            if (block is TComponent component)
            {
                return component;
            }

            throw new Exception("Block is not " + typeof(TComponent));
        }
        
        public bool TryGetBlock<TComponent>(int x, int y, out TComponent component)
        {
            if (ExistsComponentBlock<TComponent>(x,y))
            {
                component = GetBlock<TComponent>(x, y);
                return true;
            }
            component = default;
            return false;
        }

        public List<SaveBlockData> GetSaveBlockDataList()
        {
            var list = new List<SaveBlockData>();
            foreach (var block in _blockMasterDictionary)
            {
                list.Add(new SaveBlockData(
                    block.Value.X,
                    block.Value.Y,
                    block.Value.Block.BlockHash,
                    block.Value.Block.EntityId,
                    block.Value.Block.GetSaveState(),
                    (int) block.Value.BlockDirection));
            }

            return list;
        }

        public void LoadBlockDataList(List<SaveBlockData> saveBlockDataList)
        {
            foreach (var block in saveBlockDataList)
            {
                AddBlock(
                    _blockFactory.Load(block.BlockHash, block.EntityId, block.State),
                    block.X,
                    block.Y,
                    (BlockDirection) block.Direction);
            }
        }


        public bool Exists(int x, int y) { return GetBlock(x, y).BlockId != BlockConst.EmptyBlockId; }
        private int GetEntityId(int x, int y) { return _coordinateDictionary[new Coordinate(x, y)]; }
    }
}