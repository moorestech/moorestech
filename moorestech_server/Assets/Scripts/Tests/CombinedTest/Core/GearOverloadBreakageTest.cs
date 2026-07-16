using System;
using System.Collections.Generic;
using System.Threading;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using System.Reflection;
using Tests.Util;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class GearOverloadBreakageTest
    {
        [Test]
        public void OverloadedGearIsRemovedAsBroken()
        {
            // 過負荷のギアが破損扱いで除去されることを確認
            // Verify overloaded gear gets removed with Broken reason
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var generatorPos = new Vector3Int(0, 0, 1);
            var gearPos = new Vector3Int(0, 0, 0);

            // 過負荷になるギアネットワークを構築する
            // Build an overloaded gear network
            world.TryAddBlock(ForUnitTestModBlockId.SimpleFastGearGenerator, generatorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var removalReasons = new List<BlockRemoveReason>();
            using var subscription = ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(update => removalReasons.Add(update.RemoveReason));

            for (var i = 0; i < 120 && world.Exists(gearPos); i++)
            {
                GameUpdater.UpdateOneTick();
            }

            Assert.IsFalse(world.Exists(gearPos));
            Assert.IsTrue(removalReasons.Contains(BlockRemoveReason.Broken));
        }

        [Test]
        public void GearWithinThresholdRemains()
        {
            // 許容値内では破壊されないことを確認
            // Ensure gear stays when within allowed thresholds
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var gearPos = new Vector3Int(1, 0, 0);
            var generatorPos = new Vector3Int(1, 0, 1);
            world.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);

            // 過負荷閾値（RPM/トルク10）を十分下回る低RPMで駆動し、破壊されないことを確認する
            // Drive at a low RPM well under the overload thresholds (RPM/torque 10) to confirm the gear is not broken
            var generator = generatorBlock.GetComponent<SimpleGearGeneratorComponent>();
            generator.SetGenerateRpm(1f);
            generator.SetGenerateTorque(1000f);

            for (var i = 0; i < 160; i++)
            {
                GameUpdater.UpdateOneTick();
            }

            Assert.IsTrue(world.Exists(gearPos));
        }

        [Test]
        public void IdleRequestedTorqueKeepsBeltBelowBreakageThresholdTest()
        {
            // 実ブロックの過負荷設定を使い、要求倍率込みCurrentTorqueだけを変えた境界を検証する
            // Use the real block overload settings and vary only CurrentTorque including its request rate
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var fullPosition = new Vector3Int(2, 0, 0);
            var idlePosition = new Vector3Int(4, 0, 0);
            world.TryAddBlock(ForUnitTestModBlockId.SmallGearBeltConveyor, fullPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fullBlock);
            world.TryAddBlock(ForUnitTestModBlockId.SmallGearBeltConveyor, idlePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var idleBlock);

            var param = (GearBeltConveyorBlockParam)fullBlock.BlockMasterElement.BlockParam;
            var overloadParam = (IGearOverloadParam)param;
            var thresholdTorque = (float)overloadParam.OverloadMaxTorque;
            var fullTorque = thresholdTorque / (float)overloadParam.BaseDestructionProbability;
            var idleTorque = fullTorque * (float)param.GearConsumption.IdlePowerRate;
            var rpmAtThreshold = new RPM((float)overloadParam.OverloadMaxRpm);

            // 閾値10÷破断率0.5=フル20で確率1、そこへidle倍率0.3を掛けた6は閾値内となる
            // Threshold 10 / breakage rate 0.5 gives full torque 20 and chance 1; idle rate 0.3 reduces it to 6 below the limit
            var fullTransformer = new FixedCurrentGearTransformer(fullBlock.BlockInstanceId, rpmAtThreshold, new Torque(fullTorque));
            var idleTransformer = new FixedCurrentGearTransformer(idleBlock.BlockInstanceId, rpmAtThreshold, new Torque(idleTorque));
            var fullBreakage = new GearOverloadBreakageComponent(fullBlock.BlockInstanceId, fullTransformer, overloadParam);
            var idleBreakage = new GearOverloadBreakageComponent(idleBlock.BlockInstanceId, idleTransformer, overloadParam);

            fullBreakage.TickOverloadCheck();
            idleBreakage.TickOverloadCheck();

            Assert.IsTrue(fullBreakage.IsDestroy);
            Assert.IsTrue(world.Exists(fullPosition));
            Assert.IsFalse(idleBreakage.IsDestroy);
            Assert.IsTrue(world.Exists(idlePosition));

            // 破断予約は同じtickの末尾でブロックへ反映される
            // Apply the reserved break at the end of the same tick
            GameUpdater.UpdateOneTick();
            Assert.IsFalse(world.Exists(fullPosition));
            Assert.IsTrue(world.Exists(idlePosition));
            fullBreakage.Destroy();
            idleBreakage.Destroy();
        }

        private class FixedCurrentGearTransformer : IGearEnergyTransformer
        {
            public BlockInstanceId BlockInstanceId { get; }
            public RPM CurrentRpm { get; }
            public Torque CurrentTorque { get; }
            public bool IsCurrentClockwise => true;
            public bool IsDestroy { get; private set; }

            public FixedCurrentGearTransformer(BlockInstanceId blockInstanceId, RPM currentRpm, Torque currentTorque)
            {
                BlockInstanceId = blockInstanceId;
                CurrentRpm = currentRpm;
                CurrentTorque = currentTorque;
            }

            public Torque GetRequiredTorque(RPM rpm, bool isClockwise) => CurrentTorque;
            public void NotifyStateChanged() { }
            public List<GearConnect> GetGearConnects() => new();
            public void Destroy() => IsDestroy = true;
        }
    }
}
