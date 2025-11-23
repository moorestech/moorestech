using System.Collections.Generic;
using Game.Block.Event;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface.Event;
using Game.Block.Interface;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Game.Block.Factory
{
    /// <summary>
    ///     バニラのブロックの全てのテンプレートを作るクラス
    /// </summary>
    public class VanillaIBlockTemplates
    {
        public readonly Dictionary<string, IBlockTemplate> BlockTypesDictionary;
        private readonly IBlockRemover _blockRemover;

        public VanillaIBlockTemplates(IBlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent, IBlockRemover blockRemover)
        {
            _blockRemover = blockRemover;
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
            BlockTypesDictionary.Add(BlockTypeConst.ItemShooterAccelerator, new VanillaItemShooterAcceleratorTemplate(_blockRemover));
            
            BlockTypesDictionary.Add(BlockTypeConst.Gear, new VanillaGearTemplate(_blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.Shaft, new VanillaShaftTemplate(_blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.SimpleGearGenerator, new VanillaSimpleGearGeneratorTemplate(_blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.FuelGearGenerator, new VanillaFuelGearGeneratorTemplate(_blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.GearElectricGenerator, new VanillaGearElectricGeneratorTemplate(_blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.GearMiner, new VanillaGearMinerTemplate(blockInventoryEvent, _blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.GearMapObjectMiner, new VanillaGearMapObjectMinerTemplate(_blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.GearMachine, new VanillaGearMachineTemplate(blockInventoryEvent, _blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.GearBeltConveyor, new VanillaGearBeltConveyorTemplate(_blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.TrainRail, new VanillaTrainRailTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.FluidPipe, new VanillaFluidBlockTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.TrainStation, new VanillaTrainStationTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.TrainCargoPlatform, new VanillaTrainCargoTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.BaseCamp, new BaseCampBlockTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.GearPump, new VanillaGearPumpTemplate(_blockRemover));
            BlockTypesDictionary.Add(BlockTypeConst.ElectricPump, new VanillaElectricPumpTemplate());
        }
    }
}
