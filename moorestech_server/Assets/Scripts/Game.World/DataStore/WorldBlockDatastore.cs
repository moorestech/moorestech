using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
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
        //メインのデータストア
        public IReadOnlyDictionary<BlockInstanceId, WorldBlockData> BlockMasterDictionary => _blockMasterDictionary;
        private readonly Dictionary<BlockInstanceId, WorldBlockData> _blockMasterDictionary = new(); //ブロックのEntityIdとブロックの紐づけ
        //イベント
        public IObservable<(BlockState state, WorldBlockData blockData)> OnBlockStateChange => _onBlockStateChange;
        private readonly Subject<(BlockState state, WorldBlockData blockData)> _onBlockStateChange = new();
        
        private readonly Dictionary<IBlockComponent, IBlock> _blockComponentDictionary = new(); //コンポーネントとブロックの紐づけ
        
        //座標とキーの紐づけ
        private readonly Dictionary<Vector3Int, BlockInstanceId> _coordinateDictionary = new();
        private readonly IBlockFactory _blockFactory;
        
        public WorldBlockDatastore(IBlockFactory blockFactory)
        {
            _blockFactory = blockFactory;
        }
        
        public bool TryAddLoadedBlock(Guid blockGuid, BlockInstanceId blockInstanceId, Dictionary<string,string> componentStates, Vector3Int position, BlockDirection direction, out IBlock block)
        {
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockGuid).BlockSize;
            var blockPositionInfo = new BlockPositionInfo(position, direction, blockSize);
            block = _blockFactory.Load(blockGuid, blockInstanceId, componentStates, blockPositionInfo);
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
        
        public IBlock GetBlock(IBlockComponent component)
        {
            return _blockComponentDictionary.GetValueOrDefault(component);
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
        
        public bool TryAddBlock(BlockId blockId, Vector3Int position, BlockDirection direction, out IBlock block, BlockCreateParam[] createParams)
        {
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
            var blockPositionInfo = new BlockPositionInfo(position, direction, blockSize);
            block = _blockFactory.Create(blockId, BlockInstanceId.Create(), blockPositionInfo, createParams);
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
                var data = new WorldBlockData(block, pos, blockDirection);
                _blockMasterDictionary.Add(block.BlockInstanceId, data);
                _coordinateDictionary.Add(pos, block.BlockInstanceId);
                ((WorldBlockUpdateEvent)ServerContext.WorldBlockUpdateEvent).OnBlockPlaceEventInvoke(pos, data);
                
                block.BlockStateChange.Subscribe(state => { _onBlockStateChange.OnNext((state, data)); });
                
                foreach (var component in block.ComponentManager.GetComponents<IBlockComponent>())
                {
                    _blockComponentDictionary.Add(component, block);
                }
                
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
                var block = blockFactory.Load(blockSave.BlockGuid, new BlockInstanceId(blockSave.EntityId), blockSave.ComponentStates, blockData);
                
                TryAddBlock(block);
            }
        }
        
        #endregion
    }
}