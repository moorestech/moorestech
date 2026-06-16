using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.World.DataStore
{
    /// <summary>
    ///     ワールドに存在するブロックとその座標の対応づけを行います。
    /// </summary>
    public class WorldBlockDatastore : IWorldBlockDatastore
    {
        //メインのデータストア
        public IReadOnlyDictionary<BlockInstanceId, WorldBlockData> BlockMasterDictionary => _blockMasterDictionary;
        private readonly Dictionary<BlockInstanceId, WorldBlockData> _blockMasterDictionary = new(); //ブロックのEntityIdとブロックの紐づけ
        //イベント
        public IObservable<(BlockState state, WorldBlockData blockData)> OnBlockStateChange => _onBlockStateChange;
        private readonly Subject<(BlockState state, WorldBlockData blockData)> _onBlockStateChange = new();

        private readonly Dictionary<IBlockComponent, IBlock> _blockComponentDictionary = new(); //コンポーネントとブロックの紐づけ

        private readonly BlockCoordinateIndex _coordinateIndex = new();
        private readonly Dictionary<Vector3Int, BlockInstanceId> _originCoordinateDictionary = new();
        private readonly IBlockFactory _blockFactory;

        public WorldBlockDatastore(IBlockFactory blockFactory)
        {
            _blockFactory = blockFactory;
        }

        public bool RemoveBlock(Vector3Int pos, BlockRemoveReason reason)
        {
            if (!this.Exists(pos)) return false;

            var entityId = GetEntityId(pos);
            if (!_blockMasterDictionary.ContainsKey(entityId)) return false;

            var data = _blockMasterDictionary[entityId];
            ((WorldBlockUpdateEvent)ServerContext.WorldBlockUpdateEvent).OnBlockRemoveEventInvoke(pos, data, reason);

            data.Block.Destroy();
            _blockMasterDictionary.Remove(entityId);
            _coordinateIndex.Remove(data.BlockPositionInfo);
            _originCoordinateDictionary.Remove(data.BlockPositionInfo.OriginalPos);
            return true;
        }


        public IBlock GetBlock(Vector3Int pos)
        {
            return GetBlockData(pos)?.Block;
        }

        public IBlock GetBlock(IBlockComponent component)
        {
            return _blockComponentDictionary.GetValueOrDefault(component);
        }

        public WorldBlockData GetOriginPosBlock(Vector3Int pos)
        {
            return _originCoordinateDictionary.TryGetValue(pos, out var entityId)
                ? _blockMasterDictionary.TryGetValue(entityId, out var data) ? data : null
                : null;
        }

        public BlockDirection GetBlockDirection(Vector3Int pos)
        {
            var block = GetBlockData(pos);
            //TODO ブロックないときの処理どうしよう
            return block?.BlockPositionInfo.BlockDirection ?? BlockDirection.North;
        }

        public IBlock GetBlock(BlockInstanceId blockInstanceId)
        {
            return _blockMasterDictionary.TryGetValue(blockInstanceId, out var data) ? data.Block : null;
        }

        public Vector3Int GetBlockPosition(BlockInstanceId blockInstanceId)
        {
            if (_blockMasterDictionary.TryGetValue(blockInstanceId, out var data)) return data.BlockPositionInfo.OriginalPos;

            throw new Exception("ブロックがありません");
        }

        public bool TryAddBlock(BlockId blockId, Vector3Int position, BlockDirection direction, BlockCreateParam[] createParams, out IBlock block)
        {
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
            var blockPositionInfo = new BlockPositionInfo(position, direction, blockSize);
            block = _blockFactory.Create(blockId, BlockInstanceId.Create(), blockPositionInfo, createParams);
            return TryAddBlock(block, false);
        }

        private bool TryAddBlock(IBlock block, bool isInitialLoad)
        {
            var pos = block.BlockPositionInfo.OriginalPos;
            var blockDirection = block.BlockPositionInfo.BlockDirection;

            //IDが未登録で、かつ占有範囲が既存ブロックと重ならない場合のみ設置する
            //Place only when the id is unregistered and the footprint does not overlap any existing block
            if (!_blockMasterDictionary.ContainsKey(block.BlockInstanceId) &&
                !IsOverlapExistingBlock(block.BlockPositionInfo))
            {
                var data = new WorldBlockData(block, pos, blockDirection);
                _blockMasterDictionary.Add(block.BlockInstanceId, data);
                _coordinateIndex.Add(block.BlockPositionInfo, block.BlockInstanceId);
                _originCoordinateDictionary.Add(pos, block.BlockInstanceId);
                var worldBlockUpdateEvent = (WorldBlockUpdateEvent)ServerContext.WorldBlockUpdateEvent;
                if (isInitialLoad)
                    worldBlockUpdateEvent.OnInitialBlockLoadPlaceEventInvoke(pos, data);
                else
                    worldBlockUpdateEvent.OnBlockPlaceEventInvoke(pos, data);

                block.BlockStateChange.Subscribe(state => { _onBlockStateChange.OnNext((state, data)); });

                foreach (var component in block.ComponentManager.GetComponents<IBlockComponent>())
                {
                    _blockComponentDictionary.Add(component, block);
                }

                return true;
            }

            return false;
        }

        //設置しようとするブロックの占有範囲が既存ブロックと重なるかを判定する
        //Check whether the footprint of the block to place overlaps any existing block
        private bool IsOverlapExistingBlock(BlockPositionInfo positionInfo)
        {
            return _coordinateIndex.ContainsAny(positionInfo);
        }

        private BlockInstanceId GetEntityId(Vector3Int pos)
        {
            return GetBlockData(pos).Block.BlockInstanceId;
        }

        /// <summary>
        ///     TODO GetBlockは頻繁に呼ばれる訳では無いが、この方式は効率が悪いのでなにか改善したい
        /// </summary>
        private WorldBlockData GetBlockData(Vector3Int pos)
        {
            if (!_coordinateIndex.TryGetBlockInstanceId(pos, out var entityId)) return null;
            return _blockMasterDictionary.TryGetValue(entityId, out var data) ? data : null;
        }

        #region Save&Load

        public List<BlockJsonObject> GetSaveJsonObject()
        {
            var list = new List<BlockJsonObject>();
            foreach (KeyValuePair<BlockInstanceId, WorldBlockData> block in _blockMasterDictionary)
                list.Add(new BlockJsonObject(
                    block.Value.BlockPositionInfo.OriginalPos,
                    block.Value.Block.BlockGuid.ToString(),
                    block.Value.Block.BlockInstanceId.AsPrimitive(),
                    block.Value.Block.GetSaveState(),
                    (int)block.Value.BlockPositionInfo.BlockDirection));

            return list;
        }

        //TODO ここに書くべきではないのでは？セーブも含めてこの処理は別で書くべきだと思う
        public void LoadBlockDataList(List<BlockJsonObject> saveBlockDataList)
        {
            var blockFactory = ServerContext.BlockFactory;
            foreach (var blockSave in saveBlockDataList)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(blockSave.BlockGuid);

                var pos = blockSave.Pos;
                var direction = (BlockDirection)blockSave.Direction;
                var size = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;

                var blockData = new BlockPositionInfo(pos, direction, size);
                var block = blockFactory.Load(blockSave.BlockGuid, new BlockInstanceId(blockSave.InstanceId), blockSave.ComponentStates, blockData);

                TryAddBlock(block, true);
            }

            // 全てのブロックがロードされた後にIPostBlockLoadを実行する
            // Execute IPostBlockLoad after all blocks are loaded
            foreach (var blockData in _blockMasterDictionary.Values)
            {
                foreach (var component in blockData.Block.ComponentManager.GetComponents<IPostBlockLoad>())
                {
                    component.OnPostBlockLoad();
                }
            }
        }

        #endregion
    }
}
