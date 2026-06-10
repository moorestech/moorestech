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
    ///     モジュールが歯車機械の要求トルクと加工速度へ正しく作用することを検証するテスト
    ///     Tests verifying that modules correctly affect the gear machine's required torque and processing speed
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
            boostedInventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Efficiency));

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
            var rpm = new RPM(consumption.BaseRpm);
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
            var rpm = new RPM(consumption.BaseRpm);
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
            boostedInventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Efficiency));

            TickWithBasePower(consumption, (plainGear, plainProcessor), (boostedGear, boostedProcessor));
            Assert.AreEqual(ProcessState.Idle, plainProcessor.CurrentState);
            Assert.AreEqual(ProcessState.Idle, boostedProcessor.CurrentState);

            // Idle中は装着機の要求トルクが未装着機および基準計算と一致することを確認する
            // While idle, the equipped machine's required torque matches the plain machine and the base calculation
            var rpm = new RPM(consumption.BaseRpm);
            var plainTorque = plainGear.GetRequiredTorque(rpm, true).AsPrimitive();
            var boostedTorque = boostedGear.GetRequiredTorque(rpm, true).AsPrimitive();
            var expected = GearConsumptionCalculator.CalcRequiredTorque(consumption, rpm).AsPrimitive();
            Assert.AreEqual(plainTorque, boostedTorque, 0.0001f);
            Assert.AreEqual(expected, boostedTorque, 0.0001f);
        }

        [Test]
        // 倍率分のトルクが供給されるとき、スピードモジュール装着の歯車機械が未装着機より先に加工を終えることを検証する
        // Verify a speed-equipped gear machine finishes before a plain one when the network supplies the multiplier-scaled torque
        public void SpeedModuleShortensGearProcessingTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 未装着機とスピードモジュール装着機を設置し、レシピ入力を投入する
            // Place a plain machine and a speed-equipped machine and insert the recipe inputs
            var (plainInventory, plainProcessor, plainGear, consumption) = PlaceGearMachine(new Vector3Int(1, 0, 0));
            var (boostedInventory, boostedProcessor, boostedGear, _) = PlaceGearMachine(new Vector3Int(5, 0, 0));
            boostedInventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Speed));

            var recipe = GetGearMachineRecipe();
            InsertRecipeInputs(plainInventory, recipe);
            InsertRecipeInputs(boostedInventory, recipe);

            // 開始tick: 基準トルクで両機を加工開始させる（開始tickでは進行しない）
            // Start tick: begin processing on both with base torque (no progress on the start tick)
            var rpm = new RPM(consumption.BaseRpm);
            var baseTorque = new Torque(consumption.BaseTorque);
            SupplyAndUpdate(plainGear, plainProcessor, baseTorque);
            SupplyAndUpdate(boostedGear, boostedProcessor, baseTorque);
            Assert.AreEqual(ProcessState.Processing, plainProcessor.CurrentState);
            Assert.AreEqual(ProcessState.Processing, boostedProcessor.CurrentState);

            // 装着機の加工tick数が速度倍率どおりに短縮されていることを確認する
            // The boosted machine's processing ticks are shortened exactly by the speed multiplier
            var speedModule = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Speed);
            var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);
            var expectedBoostedTicks = (uint)Math.Max(1, (long)Math.Round(baseTicks * (1f / (1f + speedModule.EffectValue))));
            Assert.AreEqual(baseTicks, plainProcessor.ProcessingRecipeTicks);
            Assert.AreEqual(expectedBoostedTicks, boostedProcessor.ProcessingRecipeTicks);
            Assert.Less(expectedBoostedTicks, baseTicks);

            // 短縮時間とベース時間の中間点まで進める。装着機にはトレードオフ倍率分スケールしたトルクを毎tick供給する
            // Advance to the midpoint of the boosted and base durations, supplying the boosted machine with tradeoff-scaled torque each tick
            var powerMultiplier = 1f + speedModule.TradeoffValue;
            var scaledTorque = new Torque(consumption.BaseTorque * powerMultiplier);
            var advanceTicks = (expectedBoostedTicks + baseTicks) / 2;
            for (var i = 0u; i < advanceTicks; i++)
            {
                SupplyAndUpdate(plainGear, plainProcessor, baseTorque);
                SupplyAndUpdate(boostedGear, boostedProcessor, scaledTorque);
            }

            // 装着機は完了してIdle、未装着機はまだProcessingであることを確認する（速度モジュールが自己相殺しない）
            // The boosted machine has finished (Idle) while the plain one is still Processing (the speed module does not self-cancel)
            Assert.AreEqual(ProcessState.Idle, boostedProcessor.CurrentState);
            Assert.AreEqual(ProcessState.Processing, plainProcessor.CurrentState);

            // 装着機のアウトプットにレシピ通りの成果物が入っていることを確認する
            // The boosted machine's output contains the recipe result
            var outputItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            Assert.AreEqual(recipe.OutputItems[0].Count, CountOutputItem(boostedInventory, outputItemId));

            #region Internal

            // 指定トルクを歯車へ供給し、プロセッサを1tick更新する
            // Supply the specified torque to the gear and update the processor by one tick
            void SupplyAndUpdate(GearEnergyTransformer gear, VanillaMachineProcessorComponent processor, Torque torque)
            {
                gear.SupplyPower(rpm, torque, true);
                processor.Update();
            }

            #endregion
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
            var rpm = new RPM(consumption.BaseRpm);
            var torque = new Torque(consumption.BaseTorque);
            foreach (var (gear, processor) in machines)
            {
                gear.SupplyPower(rpm, torque, true);
                processor.Update();
            }
        }

        // 指定効果軸のテスト用モジュールアイテム（modules.jsonの定義に対応するアイテム）を生成する
        // Create a test module item of the specified effect axis (the item linked to a modules.json definition)
        private static IItemStack CreateModuleItemOfAxis(string effectAxis)
        {
            var moduleElement = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == effectAxis);
            var moduleItemId = MasterHolder.ItemMaster.GetItemId(moduleElement.ItemGuid);
            return ServerContext.ItemStackFactory.Create(moduleItemId, 1);
        }

        // アウトプットレンジ内の指定アイテムの合計数を数える
        // Count the total amount of the specified item in the output range
        private static int CountOutputItem(VanillaMachineBlockInventoryComponent inventory, ItemId itemId)
        {
            var total = 0;
            for (var i = InputSlotCount; i < InputSlotCount + OutputSlotCount; i++)
            {
                var stack = inventory.GetItem(i);
                if (stack.Id == itemId) total += stack.Count;
            }
            return total;
        }
    }
}
