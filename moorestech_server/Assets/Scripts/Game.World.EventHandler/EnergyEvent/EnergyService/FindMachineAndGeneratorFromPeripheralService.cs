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
            var machineRange = poleConfigParam.MachineConnectionRange;
            
            var startMachineX = pos.x - machineRange / 2;
            var startMachineY = pos.y - machineRange / 2;
            for (var i = startMachineX; i < startMachineX + machineRange; i++)
            for (var j = startMachineY; j < startMachineY + machineRange; j++)
            {
                var machinePos = new Vector3Int(i, j);
                
                var worldBlockDatastore = ServerContext.WorldBlockDatastore;
                //範囲内に機械がある場合
                if (worldBlockDatastore.TryGetBlock<IElectricConsumer>(machinePos, out var consumer))
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