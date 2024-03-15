using System.Collections.Generic;
using Core.EnergySystem.Electric;
using Game.Block.Config.LoadConfig.Param;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public static class FindMachineAndGeneratorFromPeripheralService
    {
        public static (List<IBlockElectricConsumer>, List<IElectricGenerator>) Find(
            Vector3Int pos, ElectricPoleConfigParam poleConfigParam,
            IWorldBlockDatastore worldBlockDatastore)
        {
            var blocks = new List<IBlockElectricConsumer>();
            var generators = new List<IElectricGenerator>();
            var machineRange = poleConfigParam.machineConnectionRange;

            var startMachineX = pos.x - machineRange / 2;
            var startMachineY = pos.y - machineRange / 2;
            for (var i = startMachineX; i < startMachineX + machineRange; i++)
            for (var j = startMachineY; j < startMachineY + machineRange; j++)
            {
                var machinePos = new Vector3Int(i, j);
                //範囲内に機械がある場合
                if (worldBlockDatastore.TryGetBlock<IBlockElectricConsumer>(machinePos, out var consumer))
                    //機械を電力セグメントに追加
                    blocks.Add(consumer);

                //範囲内に発電機がある場合
                if (worldBlockDatastore.TryGetBlock<IElectricGenerator>(machinePos, out var generator))
                    //機械を電力セグメントに追加
                    generators.Add(generator);
            }

            return (blocks, generators);
        }
    }
}