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
    }
}
