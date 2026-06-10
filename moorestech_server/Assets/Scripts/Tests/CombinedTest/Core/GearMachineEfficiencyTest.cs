using System;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.GearConsumptionModule;
using Mooresmaster.Model.MachineRecipesModule;
using Mooresmaster.Model.ModulesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    ///     省エネモジュールが歯車機械の要求トルクを下げることを検証するテスト
    ///     Tests verifying that efficiency modules reduce the gear machine's required torque
    /// </summary>
    public class GearMachineEfficiencyTest
    {
        // テスト用歯車機械のスロット構成（blocks.jsonのTestGearMachineに対応）
        // Slot layout of the test gear machine (matches TestGearMachine in blocks.json)
        private const int InputSlotCount = 2;
        private const int OutputSlotCount = 3;
        private const int ModuleRangeStart = InputSlotCount + OutputSlotCount;

        [Test]
        // 省エネモジュール装着の歯車機械が、加工中に未装着より小さい要求トルクを出すことを検証する
        // Verify a processing gear machine with an efficiency module requests less torque than one without
        public void EfficiencyModuleReducesRequiredTorqueTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 未装着機と省エネモジュール装着機を離して設置する
            // Place a plain machine and an efficiency-equipped machine apart from each other
            var (plainInventory, plainProcessor, plainGear, consumption) = PlaceGearMachine(new Vector3Int(1, 0, 0));
            var (boostedInventory, boostedProcessor, boostedGear, _) = PlaceGearMachine(new Vector3Int(5, 0, 0));
            boostedInventory.SetItem(ModuleRangeStart, CreateEfficiencyModuleItem());

            // 両機械にレシピ入力を投入して加工を開始させる
            // Insert the recipe inputs into both machines and start processing
            var recipe = GetGearMachineRecipe();
            InsertRecipeInputs(plainInventory, recipe);
            InsertRecipeInputs(boostedInventory, recipe);
            TickWithBasePower(consumption, (plainGear, plainProcessor), (boostedGear, boostedProcessor));
            Assert.AreEqual(ProcessState.Processing, plainProcessor.CurrentState);
            Assert.AreEqual(ProcessState.Processing, boostedProcessor.CurrentState);

            // 装着機の要求トルクが未装着機より小さく、比率が1/(1+effectValue)であることを確認する
            // The boosted machine requests less torque and the ratio equals 1/(1+effectValue)
            var rpm = new RPM((float)consumption.BaseRpm);
            var plainTorque = plainGear.GetRequiredTorque(rpm, true).AsPrimitive();
            var boostedTorque = boostedGear.GetRequiredTorque(rpm, true).AsPrimitive();
            Assert.Less(boostedTorque, plainTorque);

            var efficiencyModule = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Efficiency);
            Assert.AreEqual(1f / (1f + efficiencyModule.EffectValue), boostedTorque / plainTorque, 0.0001f);
        }

        [Test]
        // 未装着機の加工中の要求トルクが基準計算（GearConsumptionCalculator）と一致することを検証する
        // Verify a plain processing machine's required torque equals the base GearConsumptionCalculator result
        public void NoModuleIsNeutralForGearTorqueTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 未装着機を設置して加工を開始させる
            // Place a plain machine and start processing
            var (inventory, processor, gear, consumption) = PlaceGearMachine(new Vector3Int(1, 0, 0));
            InsertRecipeInputs(inventory, GetGearMachineRecipe());
            TickWithBasePower(consumption, (gear, processor));
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            // 要求トルクがマスタ値由来の基準計算と完全一致（倍率1.0）することを確認する
            // The required torque exactly matches the master-derived base calculation (multiplier 1.0)
            var rpm = new RPM((float)consumption.BaseRpm);
            var expected = GearConsumptionCalculator.CalcRequiredTorque(consumption, rpm).AsPrimitive();
            Assert.AreEqual(expected, gear.GetRequiredTorque(rpm, true).AsPrimitive(), 0.0001f);
        }

        [Test]
        // 省エネモジュール装着機でもIdle中（スナップショット無し）は要求トルクが中立であることを検証する
        // Verify an efficiency-equipped machine requests neutral torque while idle (no snapshot)
        public void IdleIsNeutralTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 入力を入れずに装着機と未装着機を設置し、両方ともIdleのままにする
            // Place equipped and plain machines without inputs so both stay idle
            var (plainInventory, plainProcessor, plainGear, consumption) = PlaceGearMachine(new Vector3Int(1, 0, 0));
            var (boostedInventory, boostedProcessor, boostedGear, _) = PlaceGearMachine(new Vector3Int(5, 0, 0));
            boostedInventory.SetItem(ModuleRangeStart, CreateEfficiencyModuleItem());

            TickWithBasePower(consumption, (plainGear, plainProcessor), (boostedGear, boostedProcessor));
            Assert.AreEqual(ProcessState.Idle, plainProcessor.CurrentState);
            Assert.AreEqual(ProcessState.Idle, boostedProcessor.CurrentState);

            // Idle中は装着機の要求トルクが未装着機および基準計算と一致することを確認する
            // While idle, the equipped machine's required torque matches the plain machine and the base calculation
            var rpm = new RPM((float)consumption.BaseRpm);
            var plainTorque = plainGear.GetRequiredTorque(rpm, true).AsPrimitive();
            var boostedTorque = boostedGear.GetRequiredTorque(rpm, true).AsPrimitive();
            var expected = GearConsumptionCalculator.CalcRequiredTorque(consumption, rpm).AsPrimitive();
            Assert.AreEqual(plainTorque, boostedTorque, 0.0001f);
            Assert.AreEqual(expected, boostedTorque, 0.0001f);
        }

        // テスト用歯車機械（TestGearMachine）を設置して主要コンポーネントと消費定義を返す
        // Place the test gear machine (TestGearMachine) and return its key components and consumption definition
        private static (VanillaMachineBlockInventoryComponent inventory, VanillaMachineProcessorComponent processor, GearEnergyTransformer gear, GearConsumption consumption) PlaceGearMachine(Vector3Int position)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMachine, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            var gear = block.GetComponent<GearEnergyTransformer>();
            var consumption = ((GearMachineBlockParam)block.BlockMasterElement.BlockParam).GearConsumption;
            return (inventory, processor, gear, consumption);
        }

        // テスト用歯車機械のレシピ（blocks.jsonのTestGearMachineに対応）を取得する
        // Get the recipe for the test gear machine (matches TestGearMachine in blocks.json)
        private static MachineRecipeMasterElement GetGearMachineRecipe()
        {
            var gearMachineBlockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearMachine).BlockGuid;
            return MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == gearMachineBlockGuid);
        }

        // レシピの入力アイテム1セットをインプットへ投入する
        // Insert one set of the recipe's input items into the input range
        private static void InsertRecipeInputs(VanillaMachineBlockInventoryComponent inventory, MachineRecipeMasterElement recipe)
        {
            foreach (var inputItem in recipe.InputItems)
            {
                inventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
        }

        // 1tick進めて基準RPM/トルクを供給する（既存GearMachineIoTestの駆動手順を踏襲）
        // Advance one tick and supply base RPM/torque (follows the drive pattern of GearMachineIoTest)
        private static void TickWithBasePower(GearConsumption consumption, params (GearEnergyTransformer gear, VanillaMachineProcessorComponent processor)[] machines)
        {
            GameUpdater.RunFrames(1);
            var rpm = new RPM((float)consumption.BaseRpm);
            var torque = new Torque((float)consumption.BaseTorque);
            foreach (var (gear, processor) in machines)
            {
                gear.SupplyPower(rpm, torque, true);
                processor.Update();
            }
        }

        // テスト用省エネモジュールアイテム（modules.jsonのTestEfficiencyModule）を生成する
        // Create the test efficiency module item (TestEfficiencyModule in modules.json)
        private static IItemStack CreateEfficiencyModuleItem()
        {
            var moduleElement = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Efficiency);
            var moduleItemId = MasterHolder.ItemMaster.GetItemId(moduleElement.ItemGuid);
            return ServerContext.ItemStackFactory.Create(moduleItemId, 1);
        }
    }
}
