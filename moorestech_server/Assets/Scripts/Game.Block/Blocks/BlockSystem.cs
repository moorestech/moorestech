using System;
using System.Collections.Generic;
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
        
        
        public BlockSystem(int entityId, int blockId, List<IBlockComponent> blockComponents, BlockPositionInfo blockPositionInfo)
        {
            EntityId = entityId;
            BlockPositionInfo = blockPositionInfo;
            BlockConfigData = ServerContext.BlockConfig.GetBlockConfig(blockId);
            
            _blockComponentManager = new BlockComponentManager();
            _blockComponentManager.AddComponents(blockComponents);
            
            _blockStateChange = _blockComponentManager.GetComponent<IBlockStateChange>();
            _blockStateChange?.OnChangeBlockState.Subscribe(state => { _onBlockStateChange.OnNext(state); });
        }
        
        public int EntityId { get; }
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
        
        public void Destroy()
        {
            _blockComponentManager.Destroy();
        }
        
        public bool Equals(IBlock other)
        {
            if (other is null) return false;
            return EntityId == other.EntityId;
        }
    }
}