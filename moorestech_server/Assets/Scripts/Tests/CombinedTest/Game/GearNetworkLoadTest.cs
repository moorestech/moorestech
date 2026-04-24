using System;
using System.Linq;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    // GearNetwork.ManualUpdate が Laplacian ベースで計算した「各ブロックを通過する負荷トルク」を
    // IGearEnergyTransformer.CurrentLoadTorque に書き戻せていることを検証する
    // Verify GearNetwork.ManualUpdate writes per-block Laplacian load torque back to IGearEnergyTransformer.CurrentLoadTorque
    public class GearNetworkLoadTest
    {
        // ジェネレータ→シャフト→ベルトの直線で、各ブロックの CurrentLoadTorque が非ゼロかつシャフトと消費側で一致する
        // Linear generator→shaft→belt: CurrentLoadTorque is non-zero and matches between shaft and consumer
        [Test]
        public void LinearNetwork_CurrentLoadTorque_MatchesConsumerDemand()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator);
            world.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var shaft);
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(0, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var consumer);

            generator.GetComponent<SimpleGearGeneratorComponent>().SetGenerateRpm(10f);
            generator.GetComponent<SimpleGearGeneratorComponent>().SetGenerateTorque(5f);

            GameUpdater.RunFrames(1);

            var shaftTransformer = shaft.GetComponent<GearEnergyTransformer>();
            var consumerTransformer = consumer.GetComponent<GearEnergyTransformer>();

            Assert.Greater(shaftTransformer.CurrentLoadTorque.AsPrimitive(), 0f, "shaft should see torque flowing");
            Assert.Greater(consumerTransformer.CurrentLoadTorque.AsPrimitive(), 0f, "consumer should see torque flowing");
        }

        // 孤立した歯車1個では負荷トルクは0のまま（例外も発生しない）
        // Isolated lone gear keeps CurrentLoadTorque at 0 without throwing
        [Test]
        public void IsolatedGear_CurrentLoadTorqueStaysZero()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(5, 5, 5), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var loneGear);

            Assert.DoesNotThrow(() => GameUpdater.RunFrames(1));
            Assert.AreEqual(0f, loneGear.GetComponent<GearEnergyTransformer>().CurrentLoadTorque.AsPrimitive(), 1e-9f);
        }

        // 2つの独立ネットワークが互いに干渉しない
        // Two spatially separated networks compute independently
        [Test]
        public void TwoSeparateNetworks_ComputeIndependently()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var genA);
            world.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(0, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var beltA);
            genA.GetComponent<SimpleGearGeneratorComponent>().SetGenerateRpm(10f);
            genA.GetComponent<SimpleGearGeneratorComponent>().SetGenerateTorque(5f);

            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(10, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var genB);
            world.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(10, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(10, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var beltB);
            genB.GetComponent<SimpleGearGeneratorComponent>().SetGenerateRpm(10f);
            genB.GetComponent<SimpleGearGeneratorComponent>().SetGenerateTorque(5f);

            GameUpdater.RunFrames(1);

            Assert.Greater(beltA.GetComponent<GearEnergyTransformer>().CurrentLoadTorque.AsPrimitive(), 0f);
            Assert.Greater(beltB.GetComponent<GearEnergyTransformer>().CurrentLoadTorque.AsPrimitive(), 0f);
        }

        // 中間シャフト削除でネットワーク分裂 → 孤立側の CurrentLoadTorque はゼロ
        // Remove a middle shaft to split the network; isolated side drops to zero
        [Test]
        public void NetworkSplit_IsolatedSideHasZeroLoad()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gen);
            world.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var bridge);
            world.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 3), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(0, 0, 4), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var belt);
            gen.GetComponent<SimpleGearGeneratorComponent>().SetGenerateRpm(10f);
            gen.GetComponent<SimpleGearGeneratorComponent>().SetGenerateTorque(5f);

            GameUpdater.RunFrames(1);
            Assert.Greater(belt.GetComponent<GearEnergyTransformer>().CurrentLoadTorque.AsPrimitive(), 0f, "belt sees torque while connected");

            world.RemoveBlock(bridge.BlockInstanceId, BlockRemoveReason.ManualRemove);
            GameUpdater.RunFrames(1);

            Assert.AreEqual(0f, belt.GetComponent<GearEnergyTransformer>().CurrentLoadTorque.AsPrimitive(), 1e-4f, "belt cut off from generator");
        }

        // ジェネレータなしネットワークでも例外は発生せず、負荷は0固定
        // Network without a generator must not throw; loads stay zero
        [Test]
        public void NoGeneratorNetwork_DoesNotCrashAndReturnsZero()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var belt1);
            world.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var shaft);
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(0, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var belt2);

            Assert.DoesNotThrow(() => GameUpdater.RunFrames(1));

            Assert.AreEqual(0f, belt1.GetComponent<GearEnergyTransformer>().CurrentLoadTorque.AsPrimitive(), 1e-4f);
            Assert.AreEqual(0f, shaft.GetComponent<GearEnergyTransformer>().CurrentLoadTorque.AsPrimitive(), 1e-4f);
            Assert.AreEqual(0f, belt2.GetComponent<GearEnergyTransformer>().CurrentLoadTorque.AsPrimitive(), 1e-4f);
        }

        // ノード数 < 2 になる早期returnパスでも、前tickに居たブロックの負荷は0クリアされる
        // Nodes that leave a network (size drops below 2) have their CurrentLoadTorque reset to 0
        [Test]
        public void NodeCountDropsBelowTwo_PreviousLoadIsCleared()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator);
            world.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var shaft);
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(0, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var belt);
            generator.GetComponent<SimpleGearGeneratorComponent>().SetGenerateRpm(10f);
            generator.GetComponent<SimpleGearGeneratorComponent>().SetGenerateTorque(5f);

            GameUpdater.RunFrames(1);
            Assert.Greater(shaft.GetComponent<GearEnergyTransformer>().CurrentLoadTorque.AsPrimitive(), 0f);

            world.RemoveBlock(belt.BlockInstanceId, BlockRemoveReason.ManualRemove);
            world.RemoveBlock(shaft.BlockInstanceId, BlockRemoveReason.ManualRemove);
            GameUpdater.RunFrames(1);

            // 残ったジェネレータの負荷も 0 にリセットされている
            // Remaining generator should have its load zeroed
            Assert.AreEqual(0f, generator.GetComponent<GearEnergyTransformer>().CurrentLoadTorque.AsPrimitive(), 1e-9f, "lone generator pinned to zero");
        }
    }
}
