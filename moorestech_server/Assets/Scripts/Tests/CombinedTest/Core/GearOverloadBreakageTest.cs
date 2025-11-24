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
            var pos = new Vector3Int(0, 0, 0);
            world.TryAddBlock(ForUnitTestModBlockId.SmallGear, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var removalReasons = new List<BlockRemoveReason>();
            using var subscription = ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(update => removalReasons.Add(update.RemoveReason));

            var transformer = block.GetComponent<GearEnergyTransformer>();
            for (var i = 0; i < 120 && world.Exists(pos); i++)
            {
                transformer.SupplyPower(new RPM(120), new Torque(120), true);
                GameUpdater.UpdateWithWait();
            }

            Assert.IsFalse(world.Exists(pos));
            Assert.IsTrue(removalReasons.Contains(BlockRemoveReason.Broken));
        }

        [Test]
        public void GearWithinThresholdRemains()
        {
            // 許容値内では破壊されないことを確認
            // Ensure gear stays when within allowed thresholds
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var pos = new Vector3Int(1, 0, 0);
            world.TryAddBlock(ForUnitTestModBlockId.SmallGear, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var transformer = block.GetComponent<GearEnergyTransformer>();
            for (var i = 0; i < 160; i++)
            {
                transformer.SupplyPower(new RPM(5), new Torque(5), true);
                GameUpdater.UpdateWithWait();
            }

            Assert.IsTrue(world.Exists(pos));
        }
    }
}
