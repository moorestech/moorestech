using System.Collections.Generic;
using Core.Block.Config.LoadConfig.ConfigParamGenerator;
using Core.Item;
using Core.Item.Config;

namespace Core.Block.Config.LoadConfig
{
    public class VanillaBlockConfigGenerator
    {
        /// <summary>
        ///  各ブロックのコンフィグを生成する
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, IBlockConfigParamGenerator> Generate(IItemConfig itemConfig)
        {
            var config = new Dictionary<string, IBlockConfigParamGenerator>();
            config.Add(VanillaBlockType.Machine, new MachineConfigParamGenerator());
            config.Add(VanillaBlockType.Block, new BlockConfigParamGenerator());
            config.Add(VanillaBlockType.BeltConveyor, new BeltConveyorConfigParamGenerator());
            config.Add(VanillaBlockType.ElectricPole, new ElectricPoleConfigParamGenerator());
            config.Add(VanillaBlockType.Generator, new PowerGeneratorConfigParamGenerator(itemConfig));
            config.Add(VanillaBlockType.Miner, new MinerConfigParamGenerator());
            config.Add(VanillaBlockType.Chest, new ChestConfigParamGenerator());

            return config;
        }
    }
}