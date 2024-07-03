using System.Collections.Generic;
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
            
            BlockTypesDictionary.Add(VanillaBlockType.ElectricMachine, new VanillaMachineTemplate(blockInventoryEvent));
            BlockTypesDictionary.Add(VanillaBlockType.ElectricGenerator, new VanillaPowerGeneratorTemplate());
            BlockTypesDictionary.Add(VanillaBlockType.ElectricMiner, new VanillaMinerTemplate(blockInventoryEvent));
            
            BlockTypesDictionary.Add(VanillaBlockType.ItemShooter, new VanillaItemShooterTemplate());
            
            BlockTypesDictionary.Add(VanillaBlockType.Gear, new VanillaGearTemplate());
            BlockTypesDictionary.Add(VanillaBlockType.Shaft, new VanillaShaftTemplate());
            BlockTypesDictionary.Add(VanillaBlockType.SimpleGearGenerator, new VanillaSimpleGearGeneratorTemplate());
            BlockTypesDictionary.Add(VanillaBlockType.GearMachine, new VanillaGearMachineTemplate(blockInventoryEvent));
            BlockTypesDictionary.Add(VanillaBlockType.GearBeltConveyor, new VanillaGearBeltConveyorTemplate());
        }
    }
}