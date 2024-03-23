using System;
using Core.EnergySystem;
using Game.Block.Interface;
using Game.Block.Interface;
using Game.Block.Interface.State;
using UniRx;

namespace Game.Block.Blocks.ElectricPole
{
    public abstract class VanillaEnergyTransformerBase : IEnergyTransformer, IBlock
    {
        public IBlockComponentManager ComponentManager { get; } = new BlockComponentManager();
        public BlockPositionInfo BlockPositionInfo { get; }
        public IObservable<ChangedBlockState> BlockStateChange => _onBlockStateChange;
        protected readonly Subject<ChangedBlockState> _onBlockStateChange = new();

        protected VanillaEnergyTransformerBase(int blockId, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            BlockId = blockId;
            EntityId = entityId;
            BlockHash = blockHash;
            BlockPositionInfo = blockPositionInfo;
        }

        public int BlockId { get; }
        public long BlockHash { get; }

        public string GetSaveState()
        {
            return string.Empty;
        }


        public int EntityId { get; }
    }
}