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
    // SimpleGearService.NotifyStateChangedが値変化時のみ発火することを検証する
    // Verify that SimpleGearService.NotifyStateChanged fires only when the visible state actually changed
    public class SimpleGearServiceNotifyStateChangedTest
    {
        [Test]
        // 初回の計算tickでは、値の前回履歴が無いため必ず1回発火する
        // The first calculation tick always fires once, since there is no prior notified state yet
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
        // 値が変化していないのに所属networkが再計算された場合、対象歯車の通知は発火しない
        // When the owning network recalculates without this gear's values actually changing, notification must not fire
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

            // generatorの出力は変化していないが、他メンバー起因のnetwork再計算を模して強制的に再計算対象へ登録する
            // Generator output has not changed, but force the network onto the recalculation set to simulate a recalc triggered by another member
            var generator = generatorBlock.GetComponent<IGearEnergyTransformer>();
            GearNetworkDatastore.NotifyGeneratorOutputChanged(generator);
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(firedAfterFirstTick, fired, "値が変化していないtickでは再発火しないべき");
        }

        [Test]
        // 要求トルク倍率が変化し、実際にトルク値が変わったtickでは発火する
        // A tick where the torque request rate changes and the torque value actually changes must fire
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

            // 要求トルク倍率を半減させ、実際のトルク値を変化させる
            // Halve the torque request rate, actually changing the reported torque value
            gear.SetTorqueRequestRate(0.5f);
            GameUpdater.UpdateOneTick();

            Assert.AreNotEqual(torqueBeforeChange, gear.CurrentTorque.AsPrimitive(), "テスト前提として実トルク値が変化していること");
            Assert.Greater(fired, firedAfterFirstTick, "トルク値が変化したtickでは発火するべき");
        }
    }
}
