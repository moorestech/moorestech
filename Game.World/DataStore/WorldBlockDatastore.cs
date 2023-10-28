using System;
using System.Collections.Generic;
using Core.Const;
using Core.Util;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.State;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using World.Event;

namespace World.DataStore
{
    /// <summary>
    ///     ワールドに存在するブロックとその座標の対応づけを行います。
    /// </summary>
    public class WorldBlockDatastore : IWorldBlockDatastore
    {
        private readonly IBlockFactory _blockFactory;
        private readonly IBlockConfig _blockConfig;

        //メインのデータストア
        private readonly Dictionary<int, WorldBlockData> _blockMasterDictionary = new();
        private readonly BlockPlaceEvent _blockPlaceEvent;
        private readonly BlockRemoveEvent _blockRemoveEvent;

        //座標とキーの紐づけ
        private readonly Dictionary<CoreVector2Int, int> _coordinateDictionary = new();


        private readonly IBlock _nullBlock = new NullBlock();

        public WorldBlockDatastore(IBlockPlaceEvent blockPlaceEvent, IBlockFactory blockFactory,
            IBlockRemoveEvent blockRemoveEvent, IBlockConfig blockConfig)
        {
            _blockFactory = blockFactory;
            _blockConfig = blockConfig;
            _blockRemoveEvent = (BlockRemoveEvent)blockRemoveEvent;
            _blockPlaceEvent = (BlockPlaceEvent)blockPlaceEvent;
        }

        public event Action<(ChangedBlockState state, IBlock block, int x, int y)> OnBlockStateChange;

        public bool AddBlock(IBlock block, int x, int y, BlockDirection blockDirection)
        {
            //既にキーが登録されてないか、同じ座標にブロックを置こうとしてないかをチェック
            if (!_blockMasterDictionary.ContainsKey(block.EntityId) &&
                !_coordinateDictionary.ContainsKey(new CoreVector2Int(x, y)))
            {
                var c = new CoreVector2Int(x, y);
                var data = new WorldBlockData(block, x, y, blockDirection,_blockConfig);
                _blockMasterDictionary.Add(block.EntityId, data);
                _coordinateDictionary.Add(c, block.EntityId);
                _blockPlaceEvent.OnBlockPlaceEventInvoke(new BlockPlaceEventProperties(c, data.Block, blockDirection));

                block.OnBlockStateChange += state => { OnBlockStateChange?.Invoke((state, block, x, y)); };

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
                new CoreVector2Int(x, y), data.Block));

            _blockMasterDictionary.Remove(entityId);
            _coordinateDictionary.Remove(new CoreVector2Int(x, y));
            return true;
        }


        public IBlock GetBlock(int x, int y)
        {
            var c = new CoreVector2Int(x, y);
            if (_coordinateDictionary.ContainsKey(c)) return _blockMasterDictionary[_coordinateDictionary[c]].Block;
            return _nullBlock;
        }

        public bool TryGetBlock(int x, int y, out IBlock block)
        {
            if (Exists(x, y))
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
                return (data.OriginX, data.OriginY);
            }

            throw new Exception("ブロックがありません");
        }

        public BlockDirection GetBlockDirection(int x, int y)
        {
            var c = new CoreVector2Int(x, y);
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
            if (block is TComponent component) return component;

            throw new Exception("Block is not " + typeof(TComponent));
        }

        public bool TryGetBlock<TComponent>(int x, int y, out TComponent component)
        {
            if (ExistsComponentBlock<TComponent>(x, y))
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
                list.Add(new SaveBlockData(
                    block.Value.OriginX,
                    block.Value.OriginY,
                    block.Value.Block.BlockHash,
                    block.Value.Block.EntityId,
                    block.Value.Block.GetSaveState(),
                    (int)block.Value.BlockDirection));

            return list;
        }

        public void LoadBlockDataList(List<SaveBlockData> saveBlockDataList)
        {
            foreach (var block in saveBlockDataList)
                AddBlock(
                    _blockFactory.Load(block.BlockHash, block.EntityId, block.State),
                    block.X,
                    block.Y,
                    (BlockDirection)block.Direction);
        }


        public bool Exists(int x, int y)
        {
            return GetBlock(x, y).BlockId != BlockConst.EmptyBlockId;
        }

        private int GetEntityId(int x, int y)
        {
            return _coordinateDictionary[new CoreVector2Int(x, y)];
        }
    }
}