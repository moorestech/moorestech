using Core.EnergySystem;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;
using Game.World.EventHandler.Service;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler.EnergyEvent
{
    /// <summary>
    ///     
    /// </summary>
    public class ConnectElectricPoleToElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IEnergyConsumer
        where TGenerator : IEnergyGenerator
        where TTransformer : IEnergyTransformer

    {
        private readonly IBlockConfig _blockConfig;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;


        public ConnectElectricPoleToElectricSegment(IBlockPlaceEvent blockPlaceEvent,
            IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore,
            IBlockConfig blockConfig, IWorldBlockDatastore worldBlockDatastore)
        {
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
            _blockConfig = blockConfig;
            _worldBlockDatastore = worldBlockDatastore;
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }

        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
            
            var x = blockPlaceEvent.Coordinate.X;
            var y = blockPlaceEvent.Coordinate.Y;
            if (!_worldBlockDatastore.ExistsComponentBlock<IEnergyTransformer>(x, y)) return;

            var electricPoleConfigParam = _blockConfig.GetBlockConfig(blockPlaceEvent.Block.BlockId).Param as ElectricPoleConfigParam;

            
            var electricSegment = GetAndConnectElectricSegment(x, y, electricPoleConfigParam, _worldBlockDatastore.GetBlock<IEnergyTransformer>(x, y));

            
            ConnectMachine(x, y, electricSegment, electricPoleConfigParam);
        }


        ///     
        ///      １
        ///     

        private EnergySegment GetAndConnectElectricSegment(
            int x, int y, ElectricPoleConfigParam electricPoleConfigParam, IEnergyTransformer blockElectric)
        {
            
            var electricPoles = FindElectricPoleFromPeripheralService.Find(x, y, electricPoleConfigParam, _worldBlockDatastore);

            
            var electricSegment = electricPoles.Count switch
            {
                
                0 => _worldEnergySegmentDatastore.CreateEnergySegment(),
                //1
                1 => _worldEnergySegmentDatastore.GetEnergySegment(electricPoles[0]),
                //2
                _ => ElectricSegmentMergeService.MergeAndSetDatastoreElectricSegments(_worldEnergySegmentDatastore, electricPoles)
            };
            
            electricSegment.AddEnergyTransformer(blockElectric);

            return electricSegment;
        }


        ///     

        private void ConnectMachine(int x, int y, EnergySegment segment, ElectricPoleConfigParam poleConfigParam)
        {
            var (blocks, generators) =
                FindMachineAndGeneratorFromPeripheralService.Find(x, y, poleConfigParam, _worldBlockDatastore);

            
            blocks.ForEach(segment.AddEnergyConsumer);
            generators.ForEach(segment.AddGenerator);
        }
    }
}