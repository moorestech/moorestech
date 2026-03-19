using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class RecipeEnergyOverrideTest
    {
        /// <summary>
        ///     テストデータの読み込みが成功する（バリデーション通過の確認）
        ///     Test data loads successfully (validates validation passes)
        /// </summary>
        [Test]
        public void ValidationPassesWithCorrectOverrideTest()
        {
            // テストModの読み込みが成功する = バリデーションを通過している
            // Test mod loading succeeds = validation passed
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // energyOverrideType付きのレシピが正常にロードされることを確認
            // Verify recipe with energyOverrideType loads correctly
            var recipes = MasterHolder.MachineRecipesMaster.MachineRecipes.Data;
            Assert.IsTrue(recipes.Length > 0);

            // Electricオーバーライドレシピの存在を確認
            // Verify Electric override recipe exists
            var electricRecipe = recipes.FirstOrDefault(r => r.EnergyOverrideType == MachineRecipeMasterElement.EnergyOverrideTypeConst.Electric);
            Assert.IsNotNull(electricRecipe);

            // Gearオーバーライドレシピの存在を確認
            // Verify Gear override recipe exists
            var gearRecipe = recipes.FirstOrDefault(r => r.EnergyOverrideType == MachineRecipeMasterElement.EnergyOverrideTypeConst.Gear);
            Assert.IsNotNull(gearRecipe);
        }

        /// <summary>
        ///     Electricオーバーライドが適用され、RequestPowerがレシピの値になることを確認
        ///     Verify Electric override is applied and RequestPower becomes recipe value
        /// </summary>
        [Test]
        public void ElectricMachineEnergyOverrideTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;

            // Electricオーバーライド付きレシピを取得（requiredPower: 50）
            // Get recipe with Electric override (requiredPower: 50)
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                .First(r => r.EnergyOverrideType == MachineRecipeMasterElement.EnergyOverrideTypeConst.Electric);

            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }

            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            var machineComponent = block.GetComponent<VanillaElectricMachineComponent>();

            // ブロックデフォルトのrequiredPower(100)であることを確認
            // Verify block default requiredPower (100)
            Assert.AreEqual(100f, processor.RequestPower.AsPrimitive(), 0.01f);

            // エネルギー供給してレシピ処理を開始させる
            // Supply energy to start recipe processing
            machineComponent.SupplyEnergy(new ElectricPower(10000));
            GameUpdater.UpdateOneTick();

            // レシピのオーバーライド値(50)になっていることを確認
            // Verify overridden to recipe value (50)
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(50f, processor.RequestPower.AsPrimitive(), 0.01f);
        }

        /// <summary>
        ///     Gearオーバーライドが適用され、RequestPowerがオーバーライド値のTorque×RPMになることを確認
        ///     Verify Gear override applies and RequestPower becomes overridden Torque x RPM
        /// </summary>
        [Test]
        public void GearMachineEnergyOverrideTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;

            // Gearオーバーライド付きレシピを取得（requireTorque: 3, requiredRpm: 10）
            // Get recipe with Gear override (requireTorque: 3, requiredRpm: 10)
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                .First(r => r.EnergyOverrideType == MachineRecipeMasterElement.EnergyOverrideTypeConst.Gear);

            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }

            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            var gearEnergyTransformer = block.GetComponent<GearEnergyTransformer>();

            // ブロックデフォルトのrequiredPower(Torque * RPM = 0.1 * 10 = 1)を確認
            // Verify block default requiredPower (Torque * RPM = 0.1 * 10 = 1)
            var gearMachineParam = MasterHolder.BlockMaster.GetBlockMaster(recipe.BlockGuid).BlockParam as GearMachineBlockParam;
            var defaultPower = gearMachineParam.RequireTorque * gearMachineParam.RequiredRpm;
            Assert.AreEqual(defaultPower, processor.RequestPower.AsPrimitive(), 0.01f);

            // オーバーライド値以上のギアパワーを供給してレシピ処理を開始
            // Supply gear power at override levels to start recipe processing
            var rpm = new RPM(10);
            var torque = new Torque(3);
            GameUpdater.RunFrames(1);
            gearEnergyTransformer.SupplyPower(rpm, torque, true);
            processor.Update();

            // レシピのオーバーライド値(3 * 10 = 30)になっていることを確認
            // Verify overridden to recipe value (3 * 10 = 30)
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(30f, processor.RequestPower.AsPrimitive(), 0.01f);
        }
    }
}
