using System.Collections.Generic;
using Core.Block.Config.LoadConfig.ConfigParamGenerator;

namespace Core.Block.Config.LoadConfig
{
    public class VanillaBlockConfigGenerator
    {
        public Dictionary<string, IBlockConfigParamGenerator> Generate()
        {
            var config = new Dictionary<string, IBlockConfigParamGenerator>();
            config.Add(VanillaBlockType.Machine, new MachineConfigParamGenerator());

            return config;
        }
    }
}