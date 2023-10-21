using Core.EnergySystem;
using Core.EnergySystem.Electric;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.World.EventHandler.Service;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler.EnergyEvent
{
    /// <summary>
    ///     
    /// </summary>
    public class ConnectMachineToElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IEnergyConsumer
        where TGenerator : IEnergyGenerator
        where TTransformer : IEnergyTransformer
    {
        private readonly IBlockConfig _blockConfig;
        private readonly int _maxMachineConnectionRange;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;


        public ConnectMachineToElectricSegment(IBlockPlaceEvent blockPlaceEvent,
            IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore,
            IBlockConfig blockConfig,
            MaxElectricPoleMachineConnectionRange maxElectricPoleMachineConnectionRange, IWorldBlockDatastore worldBlockDatastore)
        {
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
            _blockConfig = blockConfig;
            _worldBlockDatastore = worldBlockDatastore;
            _maxMachineConnectionRange = maxElectricPoleMachineConnectionRange.Get();
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }

        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
            
            var x = blockPlaceEvent.Coordinate.X;
            var y = blockPlaceEvent.Coordinate.Y;

            
            if (!IsElectricMachine(x, y)) return;

            
            var startMachineX = x - _maxMachineConnectionRange / 2;
            var startMachineY = y - _maxMachineConnectionRange / 2;
            for (var i = startMachineX; i < startMachineX + _maxMachineConnectionRange; i++)
            for (var j = startMachineY; j < startMachineY + _maxMachineConnectionRange; j++)
            {
                if (!_worldBlockDatastore.ExistsComponentBlock<IElectricPole>(i, j)) continue;

                
                
                ConnectToElectricPole(i, j, x, y);
            }
        }

        private bool IsElectricMachine(int x, int y)
        {
            return _worldBlockDatastore.ExistsComponentBlock<TGenerator>(x, y) ||
                   _worldBlockDatastore.ExistsComponentBlock<TConsumer>(x, y);
        }



        ///     

        private void ConnectToElectricPole(int poleX, int poleY, int machineX, int machineY)
        {
            
            var pole = _worldBlockDatastore.GetBlock<TTransformer>(poleX, poleY);
            
            var configParam =
                _blockConfig.GetBlockConfig(((IBlock)pole).BlockId).Param as ElectricPoleConfigParam;
            var range = configParam.machineConnectionRange;

            
            if (poleX - range / 2 > poleX || poleX > poleX + range / 2 || poleY - range / 2 > poleY ||
                poleY > poleY + range / 2) return;

            
            
            var segment = _worldEnergySegmentDatastore.GetEnergySegment(pole);
            if (_worldBlockDatastore.ExistsComponentBlock<TGenerator>(machineX, machineY))
                segment.AddGenerator(_worldBlockDatastore.GetBlock<TGenerator>(machineX, machineY));
            else if (_worldBlockDatastore.ExistsComponentBlock<TConsumer>(machineX, machineY)) segment.AddEnergyConsumer(_worldBlockDatastore.GetBlock<TConsumer>(machineX, machineY));
        }
    }
}