using System;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.Module.TestMod.ForUnitTestModBlockId;
using static Tests.Util.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Game.Energy
{
    /// <summary>
    ///     新設計では機械は常に単一の電力セグメントにのみ所属することを検証する
    ///     Verify that under the new design a machine always belongs to exactly one energy segment
    /// </summary>
    public class MachineMultiSegmentPowerSupplyTest
    {
        // 機械を電柱・発電機とワイヤー接続すると、機械は単一セグメントに所属し要求電力を受け取る
        // Wiring a machine to a pole and generator puts it in a single segment and it receives its requested power
        [Test]
        public void WiredMachineBelongsToSingleSegmentAndIsPowered()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var networkDatastore = serviceProvider.GetService<IElectricWireNetworkDatastore>();

            // 電柱・機械・無限発電機を設置してワイヤーで接続する
            // Place a pole, machine, and infinite generator, then wire them together
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleBlock);
            worldBlockDatastore.TryAddBlock(MachineId, Pos(2, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);
            worldBlockDatastore.TryAddBlock(InfinityGeneratorId, Pos(0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(2, 0));
            ElectricWireTestUtil.Connect(Pos(0, 0), Pos(0, 2));

            var processor = machineBlock.GetComponent<VanillaMachineProcessorComponent>();
            Assert.Greater(processor.RequestPower, 0f, "テスト機械の要求電力が0だと検証が成立しない");

            // トポロジ反映のため1tick進める
            // Advance one tick for the topology flush
            GameUpdater.UpdateOneTick();

            // 機械は電柱と同じ単一セグメントにのみ所属する（多重所属は構造的に発生しない）
            // The machine belongs only to the same single segment as the pole (multi-membership cannot occur)
            Assert.AreEqual(1, GetSegmentCount(networkDatastore));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(machineBlock.BlockInstanceId, out var machineSegment));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(poleBlock.BlockInstanceId, out var poleSegment));
            Assert.AreSame(poleSegment, machineSegment);
            Assert.AreEqual(1, GetConsumers(machineSegment).Count);

            // 数tick進めて供給を安定させる
            // Advance several ticks to stabilize the supply
            for (var i = 0; i < 3; i++) GameUpdater.UpdateOneTick();

            // 無限発電機から現在の要求電力（アイドル倍率適用後）を全量受け取れている
            // The machine receives its full current requested power (with idle rate applied) from the infinite generator
            Assert.AreEqual(processor.EffectiveRequestPower, processor.CurrentPower, 0.001f);
        }

        private static Vector3Int Pos(int x, int z)
        {
            return new Vector3Int(x, 0, z);
        }
    }
}
