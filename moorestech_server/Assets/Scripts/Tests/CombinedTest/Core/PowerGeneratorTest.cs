using System;
using System.Reflection;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class PowerGeneratorTest
    {
        private const int FuelItem1Id = 0;
        private const int FuelItem2Id = 1;
        
        [Test]
        public void UseFuelTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var blockFactory = ServerContext.BlockFactory;

            // 発電機ブロックの配置
            // Place the generator block
            var posInfo = new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GeneratorId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var powerGenerator);
            var generatorComponent = powerGenerator.GetComponent<VanillaElectricGeneratorComponent>();
            var generatorConfigParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GeneratorId).BlockParam as ElectricGeneratorBlockParam;
            var itemStackFactory = ServerContext.ItemStackFactory;

            var fuelItem1 = itemStackFactory.Create(generatorConfigParam.FuelItems[FuelItem1Id].ItemGuid, 1);
            var fuelItem2 = itemStackFactory.Create(generatorConfigParam.FuelItems[FuelItem2Id].ItemGuid, 1);
            var fuelService = GetFuelService(generatorComponent); // テストで内部状態を確認するためサービスを取得 Get the service to check the internal state in the test

            // tick数で燃焼時間を計算（余裕を持たせて計算）
            // Calculate combustion time in ticks (with margin)
            var fuelTime1 = generatorConfigParam.FuelItems[FuelItem1Id].Time;
            var fuelTicks1 = (int)((fuelTime1 + 0.1) * GameUpdater.TicksPerSecond);

            // 燃料を挿入
            // Insert fuel
            generatorComponent.InsertItem(fuelItem1);

            // 1回目のループ
            // First loop
            GameUpdater.UpdateWithWait();

            // 供給電力の確認
            // Check the supplied power
            Assert.AreEqual(generatorConfigParam.FuelItems[FuelItem1Id].Power, generatorComponent.OutputEnergy().AsPrimitive());

            // 燃料の枯渇までループ（tick数で制御）
            // Loop until fuel is exhausted (controlled by tick count)
            for (var i = 0; i < fuelTicks1; i++) GameUpdater.AdvanceTicks(1);

            // 燃料が枯渇したことをサービス側の状態で確認する
            // Confirm that the fuel has been exhausted in the service side state
            var currentFuelType = GetFuelServiceField<object>(fuelService, "_currentFuelType");
            Assert.AreEqual("None", currentFuelType.ToString());
            Assert.AreEqual(0d, generatorComponent.OutputEnergy().AsPrimitive());

            // 燃料を2個挿入
            // Insert 2 fuels
            generatorComponent.InsertItem(fuelItem1);
            generatorComponent.InsertItem(fuelItem2);

            // 燃料の1個目の枯渇までループ（tick数で制御）
            // Loop until the first fuel is exhausted (controlled by tick count)
            var fuelTicks1WithMargin = (int)((fuelTime1 + 0.3) * GameUpdater.TicksPerSecond);
            for (var i = 0; i < fuelTicks1WithMargin; i++) GameUpdater.AdvanceTicks(1);

            // サービスの現在燃料IDが2個目を指していることを確認する
            // Confirm that the current fuel ID of the service points to the second one
            currentFuelType = GetFuelServiceField<object>(fuelService, "_currentFuelType");
            Assert.AreEqual("Item", currentFuelType.ToString());
            var currentFuelItemId = GetFuelServiceField<ItemId>(fuelService, "_currentFuelItemId");
            var fuelItemId2 = MasterHolder.ItemMaster.GetItemId(generatorConfigParam.FuelItems[FuelItem2Id].ItemGuid);
            Assert.AreEqual(fuelItemId2, currentFuelItemId);
            Assert.AreEqual(generatorConfigParam.FuelItems[FuelItem2Id].Power, generatorComponent.OutputEnergy().AsPrimitive());

            // 燃料の2個目の枯渇までループ（tick数で制御）
            // Loop until the second fuel is exhausted (controlled by tick count)
            var fuelTime2 = generatorConfigParam.FuelItems[FuelItem2Id].Time;
            var fuelTicks2 = (int)((fuelTime2 + 0.1) * GameUpdater.TicksPerSecond);
            for (var i = 0; i < fuelTicks2; i++) GameUpdater.AdvanceTicks(1);

            // 2個目も消費し終わったことをサービスの状態で確認する
            currentFuelType = GetFuelServiceField<object>(fuelService, "_currentFuelType");
            Assert.AreEqual("None", currentFuelType.ToString());
            Assert.AreEqual(0d, generatorComponent.OutputEnergy().AsPrimitive());
        }
        
        [Test]
        public void InfinityGeneratorTet()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var blockFactory = ServerContext.BlockFactory;
            var posInfo = new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one);
            var powerGenerator = blockFactory.Create(ForUnitTestModBlockId.InfinityGeneratorId, new BlockInstanceId(10), posInfo);
            var generatorComponent = powerGenerator.GetComponent<VanillaElectricGeneratorComponent>();
            
            var generatorConfigParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.InfinityGeneratorId).BlockParam as ElectricGeneratorBlockParam;
            
            //1回目のループ
            GameUpdater.UpdateWithWait();
            
            //供給電力の確認
            Assert.AreEqual(generatorConfigParam.InfinityPower, generatorComponent.OutputEnergy().AsPrimitive());
        }

        [Test]
        public void UseFluidFuelTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var blockFactory = ServerContext.BlockFactory;
            var posInfo = new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one);
            var powerGenerator = blockFactory.Create(ForUnitTestModBlockId.GeneratorId, new BlockInstanceId(11), posInfo);
            var generatorComponent = powerGenerator.GetComponent<VanillaElectricGeneratorComponent>();
            var generatorConfigParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GeneratorId).BlockParam as ElectricGeneratorBlockParam;
            var fuelService = GetFuelService(generatorComponent); // 液体燃料の状態を確認するためサービスを取得

            // テスト用設定では先頭に液体燃料が1種類だけ入っているため、最初の要素を対象に検証する
            var fluidFuel = generatorConfigParam.FuelFluids[0];
            var fluidId = MasterHolder.FluidMaster.GetFluidId(fluidFuel.FluidGuid);
            var totalAmount = fluidFuel.Amount * 2;

            // 一度に2サイクル分の液体を投入し、タンクが適切に受け入れるかを確認する
            var remain = generatorComponent.AddLiquid(new FluidStack(totalAmount, fluidId), FluidContainer.Empty);
            Assert.AreEqual(0d, remain.Amount);

            GameUpdater.UpdateWithWait();

            // タンクに液体が入っており、発電が開始されていることを確認する
            var fuelContainer = GetFuelServiceField<FluidContainer>(fuelService, "_fuelFluidContainer");
            Assert.AreEqual(fluidFuel.Amount, fuelContainer.Amount);
            var currentFuelType = GetFuelServiceField<object>(fuelService, "_currentFuelType");
            Assert.AreEqual("Fluid", currentFuelType.ToString());
            Assert.AreEqual(fluidFuel.Power, generatorComponent.OutputEnergy().AsPrimitive());

            // 1サイクル目が終わるまでアップデートをまわす（tick数で制御）
            // Loop until the first cycle ends (controlled by tick count)
            var cycleTicks = (int)((fluidFuel.Time + 0.1) * GameUpdater.TicksPerSecond);
            for (var i = 0; i < cycleTicks; i++) GameUpdater.AdvanceTicks(1);

            // 継続して電力が出力されていること、燃料タイプが液体のままであることを確認する
            currentFuelType = GetFuelServiceField<object>(fuelService, "_currentFuelType");
            Assert.AreEqual(fluidFuel.Power, generatorComponent.OutputEnergy().AsPrimitive());
            Assert.AreEqual("Fluid", currentFuelType.ToString());

            // 2サイクル目も同様に経過させ、最終的に液体がすべて消費されることを検証する（tick数で制御）
            // Loop until the second cycle ends (controlled by tick count)
            for (var i = 0; i < cycleTicks; i++) GameUpdater.AdvanceTicks(1);

            // 燃料が枯渇していることをサービスの状態で確認する
            Assert.Zero(generatorComponent.OutputEnergy().AsPrimitive());
            currentFuelType = GetFuelServiceField<object>(fuelService, "_currentFuelType");
            Assert.AreEqual("None", currentFuelType.ToString());
            Assert.AreEqual(0d, fuelContainer.Amount);
        }

        private static readonly BindingFlags PrivateInstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>
        ///     テストで内部サービスにアクセスするためのヘルパー。
        /// </summary>
        private static VanillaElectricGeneratorFuelService GetFuelService(VanillaElectricGeneratorComponent component)
        {
            var field = typeof(VanillaElectricGeneratorComponent).GetField("_fuelService", PrivateInstanceFlags);
            return (VanillaElectricGeneratorFuelService)field.GetValue(component);
        }

        /// <summary>
        ///     燃料サービス内のプライベートフィールドを参照するためのヘルパー。
        /// </summary>
        private static T GetFuelServiceField<T>(VanillaElectricGeneratorFuelService service, string fieldName)
        {
            var field = typeof(VanillaElectricGeneratorFuelService).GetField(fieldName, PrivateInstanceFlags);
            return (T)field.GetValue(service);
        }
    }
}
