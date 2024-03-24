using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.State;
using Game.World.Event;
using Game.World.Interface;
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

        //メインのデータストア
        private readonly Dictionary<int, WorldBlockData> _blockMasterDictionary = new();

        private readonly BlockPlaceEvent _blockPlaceEvent;
        private readonly BlockRemoveEvent _blockRemoveEvent;

        //座標とキーの紐づけ
        private readonly Dictionary<Vector3Int, int> _coordinateDictionary = new();
        private readonly Subject<(ChangedBlockState state, WorldBlockData blockData)> _onBlockStateChange = new();
        private readonly WorldBlockUpdateEvent _worldBlockUpdateEvent;

        public WorldBlockDatastore(IBlockPlaceEvent blockPlaceEvent, IWorldBlockUpdateEvent worldBlockUpdateEvent,
            IBlockRemoveEvent blockRemoveEvent, IBlockConfig blockConfig)
        {
            _blockConfig = blockConfig;
            _blockRemoveEvent = (BlockRemoveEvent)blockRemoveEvent;
            _blockPlaceEvent = (BlockPlaceEvent)blockPlaceEvent;
            _worldBlockUpdateEvent = (WorldBlockUpdateEvent)worldBlockUpdateEvent;
        }
        //イベント
        public IObservable<(ChangedBlockState state, WorldBlockData blockData)> OnBlockStateChange => _onBlockStateChange;

        public bool AddBlock(IBlock block)
        {
            var pos = block.BlockPositionInfo.OriginalPos;
            var blockDirection = block.BlockPositionInfo.BlockDirection;

            //既にキーが登録されてないか、同じ座標にブロックを置こうとしてないかをチェック
            if (!_blockMasterDictionary.ContainsKey(block.EntityId) &&
                !_coordinateDictionary.ContainsKey(pos))
            {
                var data = new WorldBlockData(block, pos, blockDirection, _blockConfig);
                _blockMasterDictionary.Add(block.EntityId, data);
                _coordinateDictionary.Add(pos, block.EntityId);
                _blockPlaceEvent.OnBlockPlaceEventInvoke(new BlockPlaceEventProperties(pos, data.Block, blockDirection));
                _worldBlockUpdateEvent.OnBlockPlaceEventInvoke(data);

                block.BlockStateChange.Subscribe(state => { _onBlockStateChange.OnNext((state, data)); });

                return true;
            }

            return false;
        }

        public bool RemoveBlock(Vector3Int pos)
        {
            if (!this.Exists(pos)) return false;

            var entityId = GetEntityId(pos);
            if (!_blockMasterDictionary.ContainsKey(entityId)) return false;

            var data = _blockMasterDictionary[entityId];

            _blockRemoveEvent.OnBlockRemoveEventInvoke(new BlockRemoveEventProperties(pos, data.Block));
            _worldBlockUpdateEvent.OnBlockRemoveEventInvoke(data);

            _blockMasterDictionary.Remove(entityId);
            _coordinateDictionary.Remove(pos);
            return true;
        }


        public IBlock GetBlock(Vector3Int pos)
        {
            return GetBlockDatastore(pos)?.Block;
        }

        public WorldBlockData GetOriginPosBlock(Vector3Int pos)
        {
            return _coordinateDictionary.TryGetValue(pos, out var entityId)
                ? _blockMasterDictionary.TryGetValue(entityId, out var data) ? data : null
                : null;
        }

        public Vector3Int GetBlockPosition(int entityId)
        {
            if (_blockMasterDictionary.TryGetValue(entityId, out var data)) return data.BlockPositionInfo.OriginalPos;

            throw new Exception("ブロックがありません");
        }

        public BlockDirection GetBlockDirection(Vector3Int pos)
        {
            var block = GetBlockDatastore(pos);
            //TODO ブロックないときの処理どうしよう
            return block?.BlockPositionInfo.BlockDirection ?? BlockDirection.North;
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
            foreach (KeyValuePair<int, WorldBlockData> block in
                     _blockMasterDictionary.Where(block => block.Value.BlockPositionInfo.IsContainPos(pos)))
                return block.Value;

            return null;
        }

        #region Save&Load

        public List<SaveBlockData> GetSaveBlockDataList()
        {
            var list = new List<SaveBlockData>();
            foreach (KeyValuePair<int, WorldBlockData> block in _blockMasterDictionary)
                list.Add(new SaveBlockData(
                    block.Value.BlockPositionInfo.OriginalPos,
                    block.Value.Block.BlockHash,
                    block.Value.Block.EntityId,
                    block.Value.Block.GetSaveState(),
                    (int)block.Value.BlockPositionInfo.BlockDirection));

            return list;
        }

        //TODO ここに書くべきではないのでは？セーブも含めてこの処理は別で書くべきだと思う
        public void LoadBlockDataList(List<SaveBlockData> saveBlockDataList, IBlockFactory blockFactory)
        {
            foreach (var block in saveBlockDataList)
            {
                var pos = block.Pos;
                var direction = (BlockDirection)block.Direction;
                var size = _blockConfig.GetBlockConfig(block.BlockHash).BlockSize;
                var blockData = new BlockPositionInfo(pos, direction, size);
                AddBlock(blockFactory.Load(block.BlockHash, block.EntityId, block.State, blockData));
            }
        }

        #endregion
    }
}