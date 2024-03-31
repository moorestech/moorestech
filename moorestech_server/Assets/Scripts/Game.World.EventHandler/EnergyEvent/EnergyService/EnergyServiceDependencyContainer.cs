using Server.Core.EnergySystem;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.DataStore;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public class EnergyServiceDependencyContainer<TSegment> where TSegment : EnergySegment, new()
    {
        public readonly IBlockConfig BlockConfig;
        public readonly IWorldBlockDatastore WorldBlockDatastore;
        public readonly IWorldEnergySegmentDatastore<TSegment> WorldEnergySegmentDatastore;

        public EnergyServiceDependencyContainer(IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore,
            IWorldBlockDatastore worldBlockDatastore, IBlockConfig blockConfig)
        {
            WorldEnergySegmentDatastore = worldEnergySegmentDatastore;
            WorldBlockDatastore = worldBlockDatastore;
            BlockConfig = blockConfig;
        }
    }
}