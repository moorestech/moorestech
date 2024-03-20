using System;
using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Game.Block.Interface;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.State;
using Game.World.Event;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using UniRx;
using UnityEngine;

namespace Game.World.DataStore
{
    /// <summary>
    ///     ワールドに存在するブロックとその座標の対応づけを行います。
    /// </summary>
    public class WorldBlockDatastore : IWorldBlockDatastore
    {
        private readonly IBlockConfig _blockConfig;
        private readonly IBlockFactory _blockFactory;

        //メインのデータストア
        private readonly Dictionary<int, WorldBlockData> _blockMasterDictionary = new();

        private readonly BlockPlaceEvent _blockPlaceEvent;
        private readonly BlockRemoveEvent _blockRemoveEvent;

        //座標とキーの紐づけ
        private readonly Dictionary<Vector3Int, int> _coordinateDictionary = new();


        private readonly IBlock _nullBlock = new NullBlock();

        public WorldBlockDatastore(IBlockPlaceEvent blockPlaceEvent, IBlockFactory blockFactory,
            IBlockRemoveEvent blockRemoveEvent, IBlockConfig blockConfig)
        {
            _blockFactory = blockFactory;
            _blockConfig = blockConfig;
            _blockRemoveEvent = (BlockRemoveEvent)blockRemoveEvent;
            _blockPlaceEvent = (BlockPlaceEvent)blockPlaceEvent;
        }

        public event Action<(ChangedBlockState state, IBlock block, Vector3Int pos)> OnBlockStateChange;

        public bool AddBlock(IBlock block, Vector3Int pos, BlockDirection blockDirection)
        {
            //既にキーが登録されてないか、同じ座標にブロックを置こうとしてないかをチェック
            if (!_blockMasterDictionary.ContainsKey(block.EntityId) &&
                !_coordinateDictionary.ContainsKey(pos))
            {
                var data = new WorldBlockData(block, pos, blockDirection, _blockConfig);
                _blockMasterDictionary.Add(block.EntityId, data);
                _coordinateDictionary.Add(pos, block.EntityId);
                _blockPlaceEvent.OnBlockPlaceEventInvoke(new BlockPlaceEventProperties(pos, data.Block, blockDirection));

                block.BlockStateChange.Subscribe(state => { OnBlockStateChange?.Invoke((state, block, pos)); });

                return true;
            }

            return false;
        }

        public bool RemoveBlock(Vector3Int pos)
        {
            if (!Exists(pos)) return false;

            var entityId = GetEntityId(pos);
            if (!_blockMasterDictionary.ContainsKey(entityId)) return false;

            var data = _blockMasterDictionary[entityId];

            _blockRemoveEvent.OnBlockRemoveEventInvoke(new BlockRemoveEventProperties(pos, data.Block));

            _blockMasterDictionary.Remove(entityId);
            _coordinateDictionary.Remove(pos);
            return true;
        }


        public IBlock GetBlock(Vector3Int pos)
        {
            return GetBlockDatastore(pos)?.Block ?? _nullBlock;
        }

        public WorldBlockData GetOriginPosBlock(Vector3Int pos)
        {
            return _coordinateDictionary.TryGetValue(pos, out var entityId)
                ? _blockMasterDictionary.TryGetValue(entityId, out var data) ? data : null
                : null;
        }

        public bool TryGetBlock(Vector3Int pos, out IBlock block)
        {
            block = GetBlock(pos);
            block ??= _nullBlock;
            return block != _nullBlock;
        }

        public Vector3Int GetBlockPosition(int entityId)
        {
            if (_blockMasterDictionary.TryGetValue(entityId, out var data)) return data.OriginalPos;

            throw new Exception("ブロックがありません");
        }

        public BlockDirection GetBlockDirection(Vector3Int pos)
        {
            var block = GetBlockDatastore(pos);
            //TODO ブロックないときの処理どうしよう
            return block?.BlockDirection ?? BlockDirection.North;
        }


        public bool Exists(Vector3Int pos)
        {
            return GetBlock(pos).BlockId != BlockConst.EmptyBlockId;
        }

        private int GetEntityId(Vector3Int pos)
        {
            return GetBlockDatastore(pos).Block.EntityId;
        }

        /// <summary>
        ///     TODO GetBlockは頻繁に呼ばれる訳では無いが、この方式は効率が悪いのでなにか改善したい
        /// </summary>
        private WorldBlockData GetBlockDatastore(Vector3Int pos)
        {
            foreach (var block in
                     _blockMasterDictionary.Where(block => block.Value.IsContain(pos)))
                return block.Value;

            return null;
        }

        #region Component

        public bool ExistsComponentBlock<TComponent>(Vector3Int pos)
        {
            return GetBlock(pos) is TComponent;
        }

        public TComponent GetBlock<TComponent>(Vector3Int pos)
        {
            var block = GetBlock(pos);
            if (block is TComponent component) return component;

            throw new Exception("Block is not " + typeof(TComponent));
        }

        public bool TryGetBlock<TComponent>(Vector3Int pos, out TComponent component)
        {
            if (ExistsComponentBlock<TComponent>(pos))
            {
                component = GetBlock<TComponent>(pos);
                return true;
            }

            component = default;
            return false;
        }

        #endregion


        #region Save&Load

        public List<SaveBlockData> GetSaveBlockDataList()
        {
            var list = new List<SaveBlockData>();
            foreach (var block in _blockMasterDictionary)
                list.Add(new SaveBlockData(
                    block.Value.OriginalPos,
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
                    block.Pos,
                    (BlockDirection)block.Direction);
        }

        #endregion
    }
}