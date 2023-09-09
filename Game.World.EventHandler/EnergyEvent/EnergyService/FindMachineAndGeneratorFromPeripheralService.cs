using System.Collections.Generic;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Game.World.Interface.DataStore;

namespace Game.World.EventHandler.Service
{
    public static class FindMachineAndGeneratorFromPeripheralService
    {
        public static (List<IBlockElectricConsumer>,List<IPowerGenerator>) Find(
            int x,int y,ElectricPoleConfigParam poleConfigParam,
            IWorldBlockDatastore worldBlockDatastore)
        {
            var blocks = new List<IBlockElectricConsumer>();
            var generators = new List<IPowerGenerator>();
            var machineRange = poleConfigParam.machineConnectionRange;
            
            var startMachineX = x - machineRange / 2;
            var startMachineY = y - machineRange / 2;
            for (int i = startMachineX; i < startMachineX + machineRange; i++)
            {
                for (int j = startMachineY; j < startMachineY + machineRange; j++)
                {
                    //範囲内に機械がある場合
                    if (worldBlockDatastore.TryGetBlock<IBlockElectricConsumer>(i, j,out var consumer))
                    {
                        //機械を電力セグメントに追加
                        blocks.Add(consumer);
                    }

                    //範囲内に発電機がある場合
                    if (worldBlockDatastore.TryGetBlock<IPowerGenerator>(i, j,out var generator))
                    {
                        //機械を電力セグメントに追加
                        generators.Add(generator);
                    }
                }
            }

            return (blocks, generators);
        }
    }
}