using System;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    // 要求トルク倍率（SetTorqueRequestRate）が要求トルクへ反映され、networkの再計算を正しく起こすことを検証する
    // Verifies torque request rate changes are reflected in required torque and correctly trigger network recalculation
    public class GearTorqueRequestRateTest
    {
        [Test]
        public void TorqueRequestRateChangeTriggersRecalcTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            // generator + shaft の最小networkを組む
            // Build the smallest network: generator + shaft
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var shaftBlock);

            var gearTickUpdater = serviceProvider.GetService<GearTickUpdater>();
            var shaft = shaftBlock.GetComponent<GearEnergyTransformer>();

            // ウォームアップ後、shaftは10RPM・要求トルク0.1（baseTorque）で回っている
            // After warm-up the shaft spins at 10 RPM with required torque 0.1 (baseTorque)
            GameUpdater.UpdateOneTick();
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(10f, shaft.CurrentRpm.AsPrimitive(), 0.01f);
            Assert.AreEqual(0.1f, shaft.CurrentTorque.AsPrimitive(), 0.001f);

            // 倍率変更したtickだけ再計算され、要求トルクが倍率分小さくなる
            // Only the tick with a rate change recalculates, and required torque scales down by the rate
            shaft.SetTorqueRequestRate(0.5f);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, gearTickUpdater.LastTickRecalculatedNetworkCount);
            Assert.AreEqual(0.05f, shaft.CurrentTorque.AsPrimitive(), 0.001f);

            // 同値のセットは再計算を起こさない（安定tick維持）
            // Setting the same value must not schedule recalculation (stable ticks stay stable)
            shaft.SetTorqueRequestRate(0.5f);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, gearTickUpdater.LastTickRecalculatedNetworkCount);
        }

        [Test]
        public void TorqueRequestRateDropRecoversBlackoutTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var shaftBlock);

            var shaft = shaftBlock.GetComponent<GearEnergyTransformer>();
            var generator = generatorBlock.GetComponent<SimpleGearGeneratorComponent>();

            GameUpdater.UpdateOneTick();
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(10f, shaft.CurrentRpm.AsPrimitive(), 0.01f);

            // 供給動力(0.05×10=0.5)を需要(0.1×10=1)未満へ落とし、networkをblackoutさせる
            // Drop supply power (0.05 × 10 = 0.5) below demand (0.1 × 10 = 1) to black the network out
            generator.SetGenerateTorque(0.05f);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(0f, shaft.CurrentRpm.AsPrimitive(), 0.01f);

            // idle相当へ要求を下げると需要(0.3)が供給(0.5)を下回り、再計算で復帰する
            // Lowering the request to idle level brings demand (0.3) under supply (0.5), and recalculation recovers the network
            shaft.SetTorqueRequestRate(0.3f);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(10f, shaft.CurrentRpm.AsPrimitive(), 0.01f);
        }
    }
}
