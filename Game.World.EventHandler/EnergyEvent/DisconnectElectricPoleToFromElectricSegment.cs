using Core.EnergySystem;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;
using Game.World.EventHandler.EnergyEvent.EnergyService;
using Game.World.EventHandler.Service;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler.EnergyEvent
{
    /// <summary>
    ///     
    /// </summary>
    public class DisconnectElectricPoleToFromElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IEnergyConsumer
        where TGenerator : IEnergyGenerator
        where TTransformer : IEnergyTransformer
    {
        private readonly IBlockConfig _blockConfig;
        private readonly EnergyServiceDependencyContainer<TSegment> _dependencyContainer;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;


        public DisconnectElectricPoleToFromElectricSegment(
            IBlockRemoveEvent blockRemoveEvent,
            IBlockConfig blockConfig,
            IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore,
            IWorldBlockDatastore worldBlockDatastore)
        {
            _blockConfig = blockConfig;
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
            _worldBlockDatastore = worldBlockDatastore;
            blockRemoveEvent.Subscribe(OnBlockRemove);

            _dependencyContainer = new EnergyServiceDependencyContainer<TSegment>(worldEnergySegmentDatastore, worldBlockDatastore, blockConfig);
        }

        private void OnBlockRemove(BlockRemoveEventProperties blockRemoveEvent)
        {
            var x = blockRemoveEvent.Coordinate.X;
            var y = blockRemoveEvent.Coordinate.Y;

            
            
            if (!_worldBlockDatastore.TryGetBlock<TTransformer>(x, y, out var removedElectricPole)) return;


            
            var electricPoles = FindElectricPoleFromPeripheralService.Find(
                x, y, _blockConfig.GetBlockConfig(blockRemoveEvent.Block.BlockId).Param as ElectricPoleConfigParam, _worldBlockDatastore);

            
            var removedSegment = _worldEnergySegmentDatastore.GetEnergySegment(removedElectricPole);


            switch (electricPoles.Count)
            {
                
                case 0:
                    
                    _worldEnergySegmentDatastore.RemoveEnergySegment(removedSegment);
                    return;
                //1
                case 1:
                    DisconnectOneElectricPoleFromSegmentService<TSegment, TConsumer, TGenerator, TTransformer>.Disconnect(removedElectricPole, _dependencyContainer);
                    return;
                //2
                case >= 2:
                    DisconnectTwoOrMoreElectricPoleFromSegmentService<TSegment, TConsumer, TGenerator, TTransformer>.Disconnect(removedElectricPole, _dependencyContainer);
                    break;
            }
        }
    }
}