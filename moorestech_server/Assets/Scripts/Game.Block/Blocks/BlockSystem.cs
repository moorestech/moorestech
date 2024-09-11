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

namespace Game.Block.Blocks
{
    public class BlockSystem : IBlock
    {
        public BlockInstanceId BlockInstanceId { get; }
        public BlockId BlockId { get; }
        public Guid BlockGuid => BlockElement.BlockGuid;
        public BlockElement BlockElement { get; }
        public IBlockComponentManager ComponentManager => _blockComponentManager;
        public BlockPositionInfo BlockPositionInfo { get; }
        public IObservable<BlockState> BlockStateChange => _onBlockStateChange;
        
        
        private readonly BlockComponentManager _blockComponentManager = new();
        
        private readonly IBlockStateChange _blockStateChange;
        private readonly Subject<BlockState> _onBlockStateChange = new();
        private readonly IUpdatableBlockComponent[] _updatableComponents;
        private readonly IDisposable _blockUpdateDisposable;
        
        
        public BlockSystem(BlockInstanceId blockInstanceId, Guid blockGuid, List<IBlockComponent> blockComponents, BlockPositionInfo blockPositionInfo)
        {
            BlockInstanceId = blockInstanceId;
            BlockPositionInfo = blockPositionInfo;
            BlockId = MasterHolder.BlockMaster.GetBlockId(blockGuid);
            BlockElement = MasterHolder.BlockMaster.GetBlockMaster(BlockId);
            
            _blockComponentManager = new BlockComponentManager();
            _blockComponentManager.AddComponents(blockComponents);
            
            _blockStateChange = _blockComponentManager.GetComponent<IBlockStateChange>();
            _blockStateChange?.OnChangeBlockState.Subscribe(state => { _onBlockStateChange.OnNext(state); });
            
            // NOTE 他の場所からコンポーネントを追加するようになったら、このリストに追加するようにする
            _updatableComponents = blockComponents.OfType<IUpdatableBlockComponent>().ToArray();
            
            _blockUpdateDisposable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        public BlockState GetBlockState()
        {
            return _blockStateChange?.GetBlockState();
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
            _blockComponentManager.Destroy();
            _blockUpdateDisposable.Dispose();
        }
        
        public bool Equals(IBlock other)
        {
            if (other is null) return false;
            return BlockInstanceId == other.BlockInstanceId;
        }
    }
}