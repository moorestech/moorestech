using System.Collections.Generic;
using Core.EnergySystem.Electric;
using Game.Block.Config.LoadConfig.Param;
using Game.World.Interface.DataStore;

namespace Game.World.EventHandler.Service
{
    public static class FindMachineAndGeneratorFromPeripheralService
    {
        public static (List<IBlockElectricConsumer>, List<IElectricGenerator>) Find(
            int x, int y, ElectricPoleConfigParam poleConfigParam,
            IWorldBlockDatastore worldBlockDatastore)
        {
            var blocks = new List<IBlockElectricConsumer>();
            var generators = new List<IElectricGenerator>();
            var machineRange = poleConfigParam.machineConnectionRange;

            var startMachineX = x - machineRange / 2;
            var startMachineY = y - machineRange / 2;
            for (var i = startMachineX; i < startMachineX + machineRange; i++)
            for (var j = startMachineY; j < startMachineY + machineRange; j++)
            {
                
                if (worldBlockDatastore.TryGetBlock<IBlockElectricConsumer>(i, j, out var consumer))
                    
                    blocks.Add(consumer);

                
                if (worldBlockDatastore.TryGetBlock<IElectricGenerator>(i, j, out var generator))
                    
                    generators.Add(generator);
            }

            return (blocks, generators);
        }
    }
}