using System;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    // 値変化時のみ発火することを検証
    // Verify firing only on visible-state change
    public class SimpleGearServiceNotifyStateChangedTest
    {
        [Test]
        // 初回tickは前回履歴が無いため必ず発火
        // First tick always fires; no prior notified state
        public void FirstTickAlwaysNotifiesTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gearBlock);

            var fired = 0;
            using var subscription = gearBlock.GetComponent<IBlockStateObservable>().OnChangeBlockState.Subscribe(_ => fired++);

            GameUpdater.UpdateOneTick();

            Assert.AreEqual(1, fired, "初回計算tickでは必ず1回発火するべき");
        }

        [Test]
        // network再計算されても値不変なら発火しない
        // No firing when values are unchanged despite a recalc
        public void StableNetworkDoesNotRenotifyOnForcedRecalcTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            world.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gearBlock);

            var fired = 0;
            using var subscription = gearBlock.GetComponent<IBlockStateObservable>().OnChangeBlockState.Subscribe(_ => fired++);

            GameUpdater.UpdateOneTick();
            var firedAfterFirstTick = fired;

            // generator不変でも再計算を模擬
            // Simulate a recalc without generator change
            var generator = generatorBlock.GetComponent<IGearEnergyTransformer>();
            GearNetworkDatastore.NotifyGeneratorOutputChanged(generator);
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(firedAfterFirstTick, fired, "値が変化していないtickでは再発火しないべき");
        }

        [Test]
        // 要求トルク倍率変化でトルク値が変わったら発火
        // Fires when a torque request rate change alters torque
        public void TorqueChangeNotifiesTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.Teeth10RequireTorqueTestGear, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gearBlock);
            var gear = gearBlock.GetComponent<GearEnergyTransformer>();

            var fired = 0;
            using var subscription = gearBlock.GetComponent<IBlockStateObservable>().OnChangeBlockState.Subscribe(_ => fired++);

            GameUpdater.UpdateOneTick();
            var firedAfterFirstTick = fired;
            var torqueBeforeChange = gear.CurrentTorque.AsPrimitive();

            // 要求トルク倍率を半減させる
            // Halve the torque request rate
            gear.SetTorqueRequestRate(0.5f);
            GameUpdater.UpdateOneTick();

            Assert.AreNotEqual(torqueBeforeChange, gear.CurrentTorque.AsPrimitive(), "テスト前提として実トルク値が変化していること");
            Assert.Greater(fired, firedAfterFirstTick, "トルク値が変化したtickでは発火するべき");
        }
    }
}
