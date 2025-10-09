using System.Collections.Generic;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using static Game.World.EventHandler.EnergyEvent.EnergyService.ElectricConnectionRangeService;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public static class FindMachineAndGeneratorFromPeripheralService
    {
        public static (List<IElectricConsumer>, List<IElectricGenerator>) Find(Vector3Int pos, ElectricPoleBlockParam param)
        {
            var blocks = new List<IElectricConsumer>();
            var generators = new List<IElectricGenerator>();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            foreach (var machinePos in EnumerateMachineRange(pos, param))
            {
                if (worldBlockDatastore.TryGetBlock<IElectricConsumer>(machinePos, out var consumer))
                    blocks.Add(consumer);

                if (worldBlockDatastore.TryGetBlock<IElectricGenerator>(machinePos, out var generator))
                    generators.Add(generator);
            }
            
            return (blocks, generators);
        }
    }
}
