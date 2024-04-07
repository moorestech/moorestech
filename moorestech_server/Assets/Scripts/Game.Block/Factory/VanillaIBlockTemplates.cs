using System.Collections.Generic;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Miner;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface.Event;

namespace Game.Block.Factory
{
    /// <summary>
    ///     バニラのブロックの全てのテンプレートを作るクラス
    /// </summary>
    public class VanillaIBlockTemplates
    {
        public readonly Dictionary<string, IBlockTemplate> BlockTypesDictionary;

        public VanillaIBlockTemplates(IBlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            var blockInventoryEvent = blockInventoryUpdateEvent as BlockOpenableInventoryUpdateEvent;

            //TODO 動的に構築するようにする
            BlockTypesDictionary = new Dictionary<string, IBlockTemplate>();
            BlockTypesDictionary.Add(VanillaBlockType.Block, new VanillaDefaultBlock());
            BlockTypesDictionary.Add(VanillaBlockType.BeltConveyor, new VanillaBeltConveyorTemplate());
            BlockTypesDictionary.Add(VanillaBlockType.ElectricPole, new VanillaElectricPoleTemplate());
            BlockTypesDictionary.Add(VanillaBlockType.Chest, new VanillaChestTemplate());

            BlockTypesDictionary.Add(VanillaBlockType.Machine, new VanillaMachineTemplate(blockInventoryEvent, data => new VanillaElectricMachine(data)));
            BlockTypesDictionary.Add(VanillaBlockType.Generator, new VanillaPowerGeneratorTemplate(data => new VanillaElectricGenerator(data), (data, state) => new VanillaElectricGenerator(data, state)));
            BlockTypesDictionary.Add(VanillaBlockType.Miner, new VanillaMinerTemplate(blockInventoryEvent, data => new VanillaElectricMiner(data), data => new VanillaElectricMiner(data)));


            BlockTypesDictionary.Add(VanillaBlockType.GearMachine, new VanillaMachineTemplate(blockInventoryEvent, data => new VanillaGearMachine(data)));
            BlockTypesDictionary.Add(VanillaBlockType.GearGenerator, new VanillaPowerGeneratorTemplate(data => new VanillaGearGenerator(data), (data, state) => new VanillaGearGenerator(data, state)));
            BlockTypesDictionary.Add(VanillaBlockType.GearMiner, new VanillaMinerTemplate(blockInventoryEvent, data => new VanillaGearMiner(data), data => new VanillaGearMiner(data)));

            BlockTypesDictionary.Add(VanillaBlockType.GearEnergyTransformer, new VanillaGearEnergyTransformerTemplate());
        }
    }
}