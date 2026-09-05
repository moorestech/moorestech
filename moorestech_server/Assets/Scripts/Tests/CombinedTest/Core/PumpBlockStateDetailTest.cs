using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Pump;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;
using static Tests.Util.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    /// ポンプの内部タンクが機械UIと同じFluidMachineInventoryStateDetailでクライアントへ配信されることを検証する。
    /// Verifies that a pump's inner tank reaches the client through the same FluidMachineInventoryStateDetail the machine UI uses.
    /// </summary>
    public class PumpBlockStateDetailTest
    {
        // ForUnitTestModのmap.jsonで定義されたWater Vein座標
        // Water Vein coordinates defined in ForUnitTestMod map.json
        private static readonly Vector3Int WaterVeinPos = new(10, 0, 0);

        private static readonly Guid WaterFluidGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // 生成前は空、生成後は原液1本ぶんが出力タンクとして載る
        // Empty before generation, then the generated fluid rides as a single output tank
        [Test]
        public void PumpStateDetail_ExposesInnerTankAsSingleOutputTank()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 給電前のポンプは生成していないので、空タンク1本を配信する
            // An unpowered pump has generated nothing yet, so it publishes a single empty tank
            var pump = PlacePump(WaterVeinPos);
            var outputComponent = pump.GetComponent<PumpFluidOutputComponent>();

            var initial = GetFluidStateDetail(outputComponent);
            Assert.AreEqual(0, initial.InputTanks.Count, "ポンプは入力タンクを持たない");
            Assert.AreEqual(1, initial.OutputTanks.Count, "内部タンクは出力タンク1本として載る");
            Assert.AreEqual(FluidMaster.EmptyFluidId.AsPrimitive(), initial.OutputTanks[0].FluidId, "生成前は空流体");
            Assert.AreEqual(0, initial.OutputTanks[0].Amount, "生成前は残量0");

            // 給電して数tick稼働させ、配信される液種と残量が実タンクに追従することを確認する
            // Power it, run several ticks, and verify the published fluid and amount follow the real tank
            SupplyPower(pump, WaterVeinPos);
            for (var i = 0; i < 10; i++) GameUpdater.RunFrames(1);

            var generated = GetFluidStateDetail(outputComponent);
            var inventory = outputComponent.GetFluidInventory();
            Assert.AreEqual(1, inventory.Count, "内部タンクに液体が入っているはず");
            Assert.AreEqual(MasterHolder.FluidMaster.GetFluidId(WaterFluidGuid).AsPrimitive(), generated.OutputTanks[0].FluidId);
            Assert.AreEqual(inventory[0].Amount, generated.OutputTanks[0].Amount, "配信量は実タンク量と一致する");
            Assert.Greater(generated.OutputTanks[0].MaxCapacity, 0d, "容量が配信されている");
        }

        // 稼働中はステート変化が通知され、クライアントが購読で追従できる
        // Running publishes state changes so the client can follow by subscription
        [Test]
        public void PumpStateDetail_NotifiesWhileRunning()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var pump = PlacePoweredPump(WaterVeinPos);
            var stateObservable = (IBlockStateObservable)pump.GetComponent<PumpFluidOutputComponent>();

            var notifyCount = 0;
            using var subscription = stateObservable.OnChangeBlockState.Subscribe(_ => notifyCount++);

            for (var i = 0; i < 5; i++) GameUpdater.RunFrames(1);

            Assert.Greater(notifyCount, 0, "稼働中はステート変化が通知されるはず");
        }

        private static FluidMachineInventoryStateDetail GetFluidStateDetail(PumpFluidOutputComponent outputComponent)
        {
            var details = ((IBlockStateDetail)outputComponent).GetBlockStateDetails();
            Assert.AreEqual(1, details.Length, "BlockStateDetailsは単一の要素を返す");
            Assert.AreEqual(FluidMachineInventoryStateDetail.BlockStateDetailKey, details[0].Key, "機械UIと同じキーで配信する");

            return MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(details[0].Value);
        }

        private static IBlock PlacePump(Vector3Int pos)
        {
            var added = ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPump, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pump);
            Assert.IsTrue(added, $"Failed to place pump at {pos}");

            return pump;
        }

        private static IBlock PlacePoweredPump(Vector3Int pos)
        {
            var pump = PlacePump(pos);
            SupplyPower(pump, pos);

            return pump;
        }

        // ポンプを電柱へ接続し、テスト発電機でpowerRate=1.0にする
        // Wire the pump to a pole and drive powerRate to 1.0 with a test generator
        private static void SupplyPower(IBlock pump, Vector3Int pos)
        {
            var polePosition = pos + new Vector3Int(2, 0, 0);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, polePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(pos, polePosition);

            GameUpdater.UpdateOneTick();
            var networkDatastore = ServerContext.GetService<IElectricWireNetworkLookup>();
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pump.BlockInstanceId, out var segment));
            AddGenerator(segment, new TestElectricGenerator(new ElectricPower(10000), new BlockInstanceId(10)));
            GameUpdater.UpdateOneTick();
        }
    }
}
