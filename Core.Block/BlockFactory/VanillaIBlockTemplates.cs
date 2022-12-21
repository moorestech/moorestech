using System.Collections.Generic;
using Core.Block.BlockFactory.BlockTemplate;
using Core.Block.Config;
using Core.Block.Event;
using Core.Block.RecipeConfig;
using Core.Item;

namespace Core.Block.BlockFactory
{
    /// <summary>
    /// バニラのブロックの全てのテンプレートを作るクラス
    /// </summary>
    public class VanillaIBlockTemplates
    {
        public readonly Dictionary<string, IBlockTemplate> BlockTypesDictionary;

        public VanillaIBlockTemplates(
            IMachineRecipeConfig machineRecipeConfig, ItemStackFactory itemStackFactory,
            IBlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            var blockInventoryEvent = blockInventoryUpdateEvent as BlockOpenableInventoryUpdateEvent;
            
            //TODO 動的に構築するようにする
            BlockTypesDictionary = new Dictionary<string, IBlockTemplate>();
            BlockTypesDictionary.Add(VanillaBlockType.Machine, new VanillaMachineTemplate(machineRecipeConfig, itemStackFactory,blockInventoryEvent));
            BlockTypesDictionary.Add(VanillaBlockType.Block, new VanillaDefaultBlock());
            BlockTypesDictionary.Add(VanillaBlockType.BeltConveyor, new VanillaBeltConveyorTemplate(itemStackFactory));
            BlockTypesDictionary.Add(VanillaBlockType.ElectricPole, new VanillaElectricPoleTemplate());
            BlockTypesDictionary.Add(VanillaBlockType.Generator, new VanillaPowerGeneratorTemplate(itemStackFactory,blockInventoryUpdateEvent));
            BlockTypesDictionary.Add(VanillaBlockType.Miner, new VanillaMinerTemplate(itemStackFactory,blockInventoryEvent));
            BlockTypesDictionary.Add(VanillaBlockType.Chest, new VanillaChestTemplate(itemStackFactory,blockInventoryEvent));
        }
    }
}