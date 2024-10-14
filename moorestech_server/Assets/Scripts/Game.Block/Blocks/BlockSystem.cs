using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks
{
    public class BlockSystem : IBlock
    {
        public BlockInstanceId BlockInstanceId { get; }
        public BlockId BlockId { get; }
        public Guid BlockGuid => BlockMasterElement.BlockGuid;
        public BlockMasterElement BlockMasterElement { get; }
        public IBlockComponentManager ComponentManager => _blockComponentManager;
        public BlockPositionInfo BlockPositionInfo { get; }
        public IObservable<BlockState> BlockStateChange => _onBlockStateChange;
        
        
        private readonly BlockComponentManager _blockComponentManager = new();
        
        private readonly IBlockStateChange _blockStateChange;
        private readonly Subject<BlockState> _onBlockStateChange = new();
        private readonly IDisposable _blockUpdateDisposable;
        
        private readonly IUpdatableBlockComponent[] _updatableComponents;
        private readonly IBlockStateDetail[] _blockStateDetails;
        
        
        public BlockSystem(BlockInstanceId blockInstanceId, Guid blockGuid, List<IBlockComponent> blockComponents, BlockPositionInfo blockPositionInfo)
        {
            BlockInstanceId = blockInstanceId;
            BlockPositionInfo = blockPositionInfo;
            BlockId = MasterHolder.BlockMaster.GetBlockId(blockGuid);
            BlockMasterElement = MasterHolder.BlockMaster.GetBlockMaster(BlockId);
            
            _blockComponentManager = new BlockComponentManager();
            _blockComponentManager.AddComponents(blockComponents);
            
            _blockStateChange = _blockComponentManager.GetComponent<IBlockStateChange>();
            _blockStateChange?.OnChangeBlockState.Subscribe(_ => { _onBlockStateChange.OnNext(GetBlockState()); });
            
            // NOTE 他の場所からコンポーネントを追加するようになったら、このリストに追加するようにする
            _updatableComponents = blockComponents.OfType<IUpdatableBlockComponent>().ToArray();
            _blockStateDetails = blockComponents.OfType<IBlockStateDetail>().ToArray();
            
            _blockUpdateDisposable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        public BlockState GetBlockState()
        {
            if (_blockStateChange == null) return null;
            
            var state = _blockStateChange.GetBlockState();
            var detailStates = new Dictionary<string, byte[]>();
            foreach (var component in _blockStateDetails)
            {
                var detailState = component.GetBlockStateDetail();
                detailStates.Add(detailState.Key, detailState.Value);
            }
            
            return new BlockState(state, detailStates);
        }
        
        public string GetSaveState()
        {
            return _blockComponentManager.TryGetComponent<IBlockSaveState>(out var blockSaveState) ? blockSaveState.GetSaveState() : string.Empty;
        }
        
        private void Update()
        {
            foreach (var component in _updatableComponents)
            {
                component.Update();
            }
        }
        
        public void Destroy()
        {
            _blockUpdateDisposable.Dispose();
            
            try
            {
                _blockComponentManager.Destroy();
            }
            catch (Exception e)
            {
                Debug.LogError("ブロックの破壊に失敗しました。");
                Debug.LogException(e);
                throw;
            }
        }
        
        public bool Equals(IBlock other)
        {
            if (other is null) return false;
            return BlockInstanceId == other.BlockInstanceId;
        }
    }
}