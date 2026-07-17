using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Pump;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.Util.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    /// 液体マップ鉱脈（FluidMapVein）の上に置かれたポンプだけが液体を生成することを検証する。
    /// Verifies that pumps only generate fluid when placed over a registered FluidMapVein.
    /// </summary>
    public class PumpFluidVeinTest
    {
        // ForUnitTestModの map.json で定義された FluidVein 座標
        // Coordinates of FluidVein defined in ForUnitTestMod map.json
        private static readonly Vector3Int WaterVeinPos = new(10, 0, 0);
        private static readonly Vector3Int SteamVeinPos = new(20, 0, 0);
        private static readonly Vector3Int NoVeinPos = new(30, 0, 0);

        private static readonly Guid WaterFluidGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // ポンプ位置にWater Veinあり、マスタも一致 → 内部タンクに水が貯まる
        // Vein matches master entry → water accumulates
        [Test]
        public void PumpOnMatchingFluidVein_GeneratesFluid()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var pump = PlacePoweredPump(WaterVeinPos);

            // 数tick待って内部タンクに液体が溜まることを確認
            // Wait several ticks and verify fluid accumulation
            for (var i = 0; i < 10; i++) GameUpdater.RunFrames(1);

            var inventory = pump.GetComponent<PumpFluidOutputComponent>().GetFluidInventory();
            Assert.AreEqual(1, inventory.Count, "内部タンクに液体が1種類入っているはず");
            Assert.AreEqual(MasterHolder.FluidMaster.GetFluidId(WaterFluidGuid), inventory[0].FluidId);
            Assert.Greater(inventory[0].Amount, 0);
        }

        // ポンプ位置に Vein が無い → 何も生成されない
        // No vein at position → no generation
        [Test]
        public void PumpOutsideFluidVein_GeneratesNothing()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var pump = PlacePoweredPump(NoVeinPos);

            for (var i = 0; i < 10; i++) GameUpdater.RunFrames(1);

            var inventory = pump.GetComponent<PumpFluidOutputComponent>().GetFluidInventory();
            Assert.AreEqual(0, inventory.Count, "Vein無しの位置では液体は生成されないはず");
        }

        // Vein は存在するがポンプのマスタ generateFluid に含まれない液体 → 生成されない
        // Vein exists but its fluid is not in pump master → no generation
        [Test]
        public void PumpOnMismatchedFluidVein_GeneratesNothing()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // TestElectricPump は Water だけを generateFluid に持つので Steam Vein 上では生成されない
            // TestElectricPump only has Water in its generateFluid table
            var pump = PlacePoweredPump(SteamVeinPos);

            for (var i = 0; i < 10; i++) GameUpdater.RunFrames(1);

            var inventory = pump.GetComponent<PumpFluidOutputComponent>().GetFluidInventory();
            Assert.AreEqual(0, inventory.Count, "マスタに一致するfluidGuidが無ければ生成されないはず");
        }

        private static IBlock PlacePoweredPump(Vector3Int pos)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var added = worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPump, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pump);
            Assert.IsTrue(added, $"Failed to place pump at {pos}");

            // ポンプを電柱へ接続して電力網を成立させる
            // Connect the pump to a pole so it belongs to a usable electric network
            var polePosition = pos + new Vector3Int(2, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, polePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(pos, polePosition);

            // ポンプが属するワイヤーセグメントへテスト発電機を登録し powerRate=1.0 にする
            // Register a test generator into the pump's wire segment so powerRate = 1.0
            GameUpdater.UpdateOneTick();
            var networkDatastore = ServerContext.GetService<IElectricWireNetworkLookup>();
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pump.BlockInstanceId, out var segment));
            AddGenerator(segment, new TestElectricGenerator(new ElectricPower(10000), new BlockInstanceId(10)));
            GameUpdater.UpdateOneTick();

            return pump;
        }
    }
}
