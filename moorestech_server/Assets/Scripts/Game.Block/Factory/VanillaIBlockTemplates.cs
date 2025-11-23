using System.Collections.Generic;
using Game.Block.Event;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
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
        
        public VanillaIBlockTemplates(IBlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent, IBlockRemover blockRemover)
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
            BlockTypesDictionary.Add(BlockTypeConst.ItemShooterAccelerator, new VanillaItemShooterAcceleratorTemplate());
            
            BlockTypesDictionary.Add(BlockTypeConst.Gear, new VanillaGearTemplate(blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.Shaft, new VanillaShaftTemplate(blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.SimpleGearGenerator, new VanillaSimpleGearGeneratorTemplate(blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.FuelGearGenerator, new VanillaFuelGearGeneratorTemplate(blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.GearElectricGenerator, new VanillaGearElectricGeneratorTemplate(blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.GearMiner, new VanillaGearMinerTemplate(blockInventoryEvent, blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.GearMapObjectMiner, new VanillaGearMapObjectMinerTemplate(blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.GearMachine, new VanillaGearMachineTemplate(blockInventoryEvent, blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.GearBeltConveyor, new VanillaGearBeltConveyorTemplate(blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.TrainRail, new VanillaTrainRailTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.FluidPipe, new VanillaFluidBlockTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.TrainStation, new VanillaTrainStationTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.TrainCargoPlatform, new VanillaTrainCargoTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.BaseCamp, new BaseCampBlockTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.GearPump, new VanillaGearPumpTemplate(blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.ElectricPump, new VanillaElectricPumpTemplate());
        }
    }
}
