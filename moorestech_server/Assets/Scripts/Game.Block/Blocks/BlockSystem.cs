using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using UniRx;

namespace Game.Block.Blocks
{
    public class BlockSystem : IBlock
    {
        private readonly BlockComponentManager _blockComponentManager = new();
        
        private readonly IBlockStateChange _blockStateChange;
        private readonly Subject<BlockState> _onBlockStateChange = new();
        private readonly IUpdatableBlockComponent[] _updatableComponents;
        private readonly IDisposable _blockUpdateDisposable;
        
        
        public BlockSystem(BlockInstanceId blockInstanceId, int blockId, List<IBlockComponent> blockComponents, BlockPositionInfo blockPositionInfo)
        {
            BlockInstanceId = blockInstanceId;
            BlockPositionInfo = blockPositionInfo;
            BlockConfigData = ServerContext.BlockConfig.GetBlockConfig(blockId);
            
            _blockComponentManager = new BlockComponentManager();
            _blockComponentManager.AddComponents(blockComponents);
            
            _blockStateChange = _blockComponentManager.GetComponent<IBlockStateChange>();
            _blockStateChange?.OnChangeBlockState.Subscribe(state => { _onBlockStateChange.OnNext(state); });
            _updatableComponents = blockComponents.OfType<IUpdatableBlockComponent>().ToArray();
            
            _blockUpdateDisposable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        public BlockInstanceId BlockInstanceId { get; }
        public int BlockId => BlockConfigData.BlockId;
        public long BlockHash => BlockConfigData.BlockHash;
        public BlockConfigData BlockConfigData { get; }
        public IBlockComponentManager ComponentManager => _blockComponentManager;
        public BlockPositionInfo BlockPositionInfo { get; }
        
        public IObservable<BlockState> BlockStateChange => _onBlockStateChange;
        
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