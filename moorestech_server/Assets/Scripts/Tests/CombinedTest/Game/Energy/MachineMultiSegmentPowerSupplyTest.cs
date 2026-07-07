using System;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Module.TestMod.ForUnitTestModBlockId;

namespace Tests.CombinedTest.Game.Energy
{
    /// <summary>
    ///     1つの機械が複数の電力セグメントに登録されたときの電力供給を検証する
    ///     Verify power supply when a single machine is registered in multiple energy segments
    /// </summary>
    public class MachineMultiSegmentPowerSupplyTest
    {
        // 発電機なしセグメントの0供給が、発電機ありセグメントからの供給を打ち消さないこと
        // A generator-less segment's zero supply must not cancel the supply from a segment that has a generator
        [Test]
        public void GeneratorlessSegmentDoesNotZeroOutSharedMachinePower()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            // 機械を設置（電柱を置かないので自動セグメント生成は発生しない）
            // Place a machine (no pole placed, so no automatic segment is created)
            worldBlockDatastore.TryAddBlock(MachineId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);
            var consumer = machineBlock.GetComponent<IElectricConsumer>();
            var processor = machineBlock.GetComponent<VanillaMachineProcessorComponent>();

            Assert.Greater(processor.RequestPower, 0f, "テスト機械の要求電力が0だと検証が成立しない");

            // 機械の要求電力を十分賄えるテスト用無限発電機を用意する
            // Prepare an infinite test generator that fully covers the machine's request power
            var generator = new TestElectricGenerator(new ElectricPower(processor.RequestPower * 10f), BlockInstanceId.Create());

            var segmentDatastore = serviceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();

            // 機械を「発電機ありセグメント」と「発電機なしセグメント」の両方に登録（二重登録の再現）
            // Register the machine into both a segment with a generator and one without (reproducing double-registration)
            var segmentWithGenerator = segmentDatastore.CreateEnergySegment();
            segmentWithGenerator.AddEnergyConsumer(consumer);
            segmentWithGenerator.AddGenerator(generator);

            var segmentWithoutGenerator = segmentDatastore.CreateEnergySegment();
            segmentWithoutGenerator.AddEnergyConsumer(consumer);

            // 数tick進めて供給を安定させる
            // Advance several ticks to stabilize the supply
            for (var i = 0; i < 3; i++) GameUpdater.UpdateOneTick();

            // 発電機なしセグメントに打ち消されず、現在の要求電力を受け取れている
            // The machine receives its current requested power without being zeroed by the generator-less segment
            Assert.AreEqual(processor.EffectiveRequestPower, processor.CurrentPower, 0.001f);
        }
    }
}
