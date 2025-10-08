using System.Collections.Generic;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public static class FindMachineAndGeneratorFromPeripheralService
    {
        public static (List<IElectricConsumer>, List<IElectricGenerator>) Find(Vector3Int pos, ElectricPoleBlockParam poleConfigParam)
        {
            var blocks = new List<IElectricConsumer>();
            var generators = new List<IElectricGenerator>();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            foreach (var machinePos in EnumerateRange(pos, poleConfigParam.MachineConnectionRange,
                         poleConfigParam.MachineConnectionHeightRange))
            {
                if (worldBlockDatastore.TryGetBlock<IElectricConsumer>(machinePos, out var consumer))
                    blocks.Add(consumer);

                if (worldBlockDatastore.TryGetBlock<IElectricGenerator>(machinePos, out var generator))
                    generators.Add(generator);
            }
            
            return (blocks, generators);

            #region Internal

            static IEnumerable<Vector3Int> EnumerateRange(Vector3Int center, int horizontalRange, int heightRange)
            {
                horizontalRange = Mathf.Max(horizontalRange, 1);
                heightRange = Mathf.Max(heightRange, 1);

                var startX = center.x - horizontalRange / 2;
                var startZ = center.z - horizontalRange / 2;
                var startY = center.y - heightRange / 2;

                var endX = startX + horizontalRange;
                var endZ = startZ + horizontalRange;
                var endY = startY + heightRange;

                for (var x = startX; x < endX; x++)
                for (var y = startY; y < endY; y++)
                for (var z = startZ; z < endZ; z++)
                    yield return new Vector3Int(x, y, z);
            }

            #endregion
        }
    }
}
