using System.Collections.Generic;
using Game.Block.Config.LoadConfig.ConfigParamGenerator;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig
{
    public class VanillaBlockConfigGenerator
    {
        /// <summary>
        ///     各ブロックのコンフィグを生成する
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, IBlockConfigParamGenerator> Generate()
        {
            var config = new Dictionary<string, IBlockConfigParamGenerator>();
            config.Add(VanillaBlockType.Machine, new MachineConfigParamGenerator());
            config.Add(VanillaBlockType.Block, new BlockConfigParamGenerator());
            config.Add(VanillaBlockType.BeltConveyor, new BeltConveyorConfigParamGenerator());
            config.Add(VanillaBlockType.ElectricPole, new ElectricPoleConfigParamGenerator());
            config.Add(VanillaBlockType.Generator, new PowerGeneratorConfigParamGenerator());
            config.Add(VanillaBlockType.Miner, new MinerConfigParamGenerator());
            config.Add(VanillaBlockType.Chest, new ChestConfigParamGenerator());

            config.Add(VanillaBlockType.GearMachine, new MachineConfigParamGenerator());
            config.Add(VanillaBlockType.GearGenerator, new PowerGeneratorConfigParamGenerator());
            config.Add(VanillaBlockType.GearMiner, new MinerConfigParamGenerator());
            config.Add(VanillaBlockType.GearEnergyTransformer, new ElectricPoleConfigParamGenerator());

            return config;
        }
    }
}