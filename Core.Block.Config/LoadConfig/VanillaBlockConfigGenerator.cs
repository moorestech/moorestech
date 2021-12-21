using System.Collections.Generic;
using Core.Block.Config.LoadConfig.ConfigParamGenerator;

namespace Core.Block.Config.LoadConfig
{
    public class VanillaBlockConfigGenerator
    {
        /// <summary>
        ///  各ブロックのコンフィグを生成する
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, IBlockConfigParamGenerator> Generate()
        {
            var config = new Dictionary<string, IBlockConfigParamGenerator>();
            config.Add(VanillaBlockType.Machine, new MachineConfigParamGenerator());
            config.Add(VanillaBlockType.Block, new BlockConfigParamGenerator());
            config.Add(VanillaBlockType.BeltConveyor, new BeltConveyorConfigParamGenerator());

            return config;
        }
    }
}