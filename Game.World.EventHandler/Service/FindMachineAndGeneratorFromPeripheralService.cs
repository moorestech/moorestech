using System.Collections.Generic;
using Core.Electric;
using Game.World.Interface.DataStore;

namespace Game.World.EventHandler.Service
{
    public class FindMachineAndGeneratorFromPeripheralService
    {
        public (List<IBlockElectric>,List<IPowerGenerator>) Find(
            int x,int y,int machineRange,
            IWorldBlockComponentDatastore<IBlockElectric> electricDatastore,
            IWorldBlockComponentDatastore<IPowerGenerator> powerGeneratorDatastore)
        {
            var blocks = new List<IBlockElectric>();
            var generators = new List<IPowerGenerator>();
            
            var startMachineX = x - machineRange / 2;
            var startMachineY = y - machineRange / 2;
            for (int i = startMachineX; i < startMachineX + machineRange; i++)
            {
                for (int j = startMachineY; j < startMachineY + machineRange; j++)
                {
                    //範囲内に機械がある場合
                    if (electricDatastore.ExistsComponentBlock(i, j))
                    {
                        //機械を電力セグメントに追加
                        blocks.Add(electricDatastore.GetBlock(i, j));
                    }

                    //範囲内に発電機がある場合
                    if (powerGeneratorDatastore.ExistsComponentBlock(i, j))
                    {
                        //機械を電力セグメントに追加
                        generators.Add(powerGeneratorDatastore.GetBlock(i, j));
                    }
                }
            }

            return (blocks, generators);
        }
    }
}