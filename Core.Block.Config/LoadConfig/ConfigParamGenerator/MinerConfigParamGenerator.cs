using System.Collections.Generic;
using Core.Block.Config.LoadConfig.Param;

namespace Core.Block.Config.LoadConfig.ConfigParamGenerator
{
    public class MinerConfigParamGenerator: IBlockConfigParamGenerator
    {
        public BlockConfigParamBase Generate(dynamic blockParam)
        {
            int requiredPower = blockParam.requiredPower;
            var oreSetting = new List<OreSetting>();
            foreach (var ore in blockParam.oreSetting)
            {
                int id = ore.oreId;
                int time = ore.time;
                oreSetting.Add(new OreSetting(id,time));
            }
            return new MinerBlockConfigParam(requiredPower,oreSetting);
        }
    }
}