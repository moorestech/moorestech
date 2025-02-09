using System.Collections.Generic;
using Game.Block.Event;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface.Event;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

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
            BlockTypesDictionary.Add(BlockTypeConst.Block, new VanillaDefaultBlock());
            BlockTypesDictionary.Add(BlockTypeConst.BeltConveyor, new VanillaBeltConveyorTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.ElectricPole, new VanillaElectricPoleTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.Chest, new VanillaChestTemplate());
            
            BlockTypesDictionary.Add(BlockTypeConst.ElectricMachine, new VanillaMachineTemplate(blockInventoryEvent));
            BlockTypesDictionary.Add(BlockTypeConst.ElectricGenerator, new VanillaPowerGeneratorTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.ElectricMiner, new VanillaMinerTemplate(blockInventoryEvent));
            
            BlockTypesDictionary.Add(BlockTypeConst.ItemShooter, new VanillaItemShooterTemplate());
            
            BlockTypesDictionary.Add(BlockTypeConst.Gear, new VanillaGearTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.Shaft, new VanillaShaftTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.SimpleGearGenerator, new VanillaSimpleGearGeneratorTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.GearMiner, new VanillaGearMinerTemplate(blockInventoryEvent));
            BlockTypesDictionary.Add(BlockTypeConst.GearMapObjectMiner, new VanillaGearMapObjectMinerTemplate(blockInventoryEvent));
            BlockTypesDictionary.Add(BlockTypeConst.GearMachine, new VanillaGearMachineTemplate(blockInventoryEvent));
            BlockTypesDictionary.Add(BlockTypeConst.GearBeltConveyor, new VanillaGearBeltConveyorTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.TrainRail, new VanillaTrainRailTemplate());
    }
    }
}