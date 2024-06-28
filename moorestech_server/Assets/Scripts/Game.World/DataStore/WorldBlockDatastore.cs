using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Extension;
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
        private readonly IBlockConfig _blockConfig;
        private readonly IBlockFactory _blockFactory;
        private readonly Dictionary<BlockInstanceId, WorldBlockData> _blockMasterDictionary = new(); //ブロックのEntityIdとブロックの紐づけ
        
        //座標とキーの紐づけ
        private readonly Dictionary<Vector3Int, BlockInstanceId> _coordinateDictionary = new();
        
        private readonly Subject<(BlockState state, WorldBlockData blockData)> _onBlockStateChange = new();
        
        public WorldBlockDatastore(IBlockFactory blockFactory, IBlockConfig blockConfig)
        {
            _blockFactory = blockFactory;
            _blockConfig = blockConfig;
        }
        
        //メインのデータストア
        public IReadOnlyDictionary<BlockInstanceId, WorldBlockData> BlockMasterDictionary => _blockMasterDictionary;
        
        //イベント
        public IObservable<(BlockState state, WorldBlockData blockData)> OnBlockStateChange => _onBlockStateChange;
        
        public bool TryAddLoadedBlock(long blockHash, BlockInstanceId blockInstanceId, string state, Vector3Int position, BlockDirection direction, out IBlock block)
        {
            var blockPositionInfo = new BlockPositionInfo(position, direction, _blockConfig.GetBlockConfig(blockHash).BlockSize);
            block = _blockFactory.Load(blockHash, blockInstanceId, state, blockPositionInfo);
            return TryAddBlock(block);
        }
        public bool RemoveBlock(Vector3Int pos)
        {
            if (!this.Exists(pos)) return false;
            
            var entityId = GetEntityId(pos);
            if (!_blockMasterDictionary.ContainsKey(entityId)) return false;
            
            var data = _blockMasterDictionary[entityId];
            ((WorldBlockUpdateEvent)ServerContext.WorldBlockUpdateEvent).OnBlockRemoveEventInvoke(pos, data);
            
            data.Block.Destroy();
            _blockMasterDictionary.Remove(entityId);
            _coordinateDictionary.Remove(pos);
            return true;
        }
        
        
        public IBlock GetBlock(Vector3Int pos)
        {
            return GetBlockData(pos)?.Block;
        }
        
        public WorldBlockData GetOriginPosBlock(Vector3Int pos)
        {
            return _coordinateDictionary.TryGetValue(pos, out var entityId)
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
        
        public bool TryAddBlock(int blockId, Vector3Int position, BlockDirection direction, out IBlock block, BlockInstanceId blockInstanceId = default)
        {
            if (blockInstanceId == default) blockInstanceId = BlockInstanceId.Create();
            var blockPositionInfo = new BlockPositionInfo(position, direction, _blockConfig.GetBlockConfig(blockId).BlockSize);
            block = _blockFactory.Create(blockId, blockInstanceId, blockPositionInfo);
            return TryAddBlock(block);
        }
        
        private bool TryAddBlock(IBlock block)
        {
            var pos = block.BlockPositionInfo.OriginalPos;
            var blockDirection = block.BlockPositionInfo.BlockDirection;
            
            //既にキーが登録されてないか、同じ座標にブロックを置こうとしてないかをチェック
            if (!_blockMasterDictionary.ContainsKey(block.BlockInstanceId) &&
                !_coordinateDictionary.ContainsKey(pos))
            {
                var data = new WorldBlockData(block, pos, blockDirection, ServerContext.BlockConfig);
                _blockMasterDictionary.Add(block.BlockInstanceId, data);
                _coordinateDictionary.Add(pos, block.BlockInstanceId);
                ((WorldBlockUpdateEvent)ServerContext.WorldBlockUpdateEvent).OnBlockPlaceEventInvoke(pos, data);
                
                block.BlockStateChange.Subscribe(state => { _onBlockStateChange.OnNext((state, data)); });
                
                return true;
            }
            
            return false;
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
            foreach (KeyValuePair<BlockInstanceId, WorldBlockData> block in
                     _blockMasterDictionary.Where(block => block.Value.BlockPositionInfo.IsContainPos(pos)))
                return block.Value;
            
            return null;
        }
        
        #region Save&Load
        
        public List<BlockJsonObject> GetSaveJsonObject()
        {
            var list = new List<BlockJsonObject>();
            foreach (KeyValuePair<BlockInstanceId, WorldBlockData> block in _blockMasterDictionary)
                list.Add(new BlockJsonObject(
                    block.Value.BlockPositionInfo.OriginalPos,
                    block.Value.Block.BlockHash,
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
                var pos = blockSave.Pos;
                var direction = (BlockDirection)blockSave.Direction;
                var size = ServerContext.BlockConfig.GetBlockConfig(blockSave.BlockHash).BlockSize;
                var blockData = new BlockPositionInfo(pos, direction, size);
                Debug.Log($"BlockLoad {blockSave.EntityId}");
                var block = blockFactory.Load(blockSave.BlockHash, new BlockInstanceId(blockSave.EntityId), blockSave.State, blockData);
                
                TryAddBlock(block);
            }
        }
        
        #endregion
    }
}