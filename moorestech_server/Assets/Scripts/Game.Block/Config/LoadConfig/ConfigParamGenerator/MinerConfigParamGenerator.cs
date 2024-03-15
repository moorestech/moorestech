using System.Collections.Generic;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.ConfigParamGenerator
{
    public class MinerConfigParamGenerator : IBlockConfigParamGenerator
    {
        public IBlockConfigParam Generate(dynamic blockParam)
        {
            int requiredPower = blockParam.requiredPower;
            int outputSlot = blockParam.outputSlot;
            var oreSetting = new List<OreSetting>();
            foreach (var ore in blockParam.oreSetting)
            {
                int id = ore.oreId;
                int time = ore.time;
                oreSetting.Add(new OreSetting(id, time));
            }

            return new MinerBlockConfigParam(requiredPower, oreSetting, outputSlot);
        }
    }
}