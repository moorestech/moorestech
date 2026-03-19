using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class GearBeltConveyorOverloadBreakageTest
    {
        [Test]
        public void OverloadedGearBeltConveyorIsRemovedAsBroken()
        {
            // 過負荷の歯車ベルトコンベアが破損扱いで除去されることを確認
            // Verify overloaded gear belt conveyor gets removed with Broken reason
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var generatorPos = new Vector3Int(0, 0, 1);
            var conveyorPos = new Vector3Int(0, 0, 0);

            // 過負荷になるギアネットワークを構築する（高速ジェネレータ + 低閾値コンベア）
            // Build an overloaded gear network (fast generator + low-threshold conveyor)
            world.TryAddBlock(ForUnitTestModBlockId.SimpleFastGearGenerator, generatorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.SmallGearBeltConveyor, conveyorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 最初のティックで破壊される可能性があるため、ティック進行前に購読を登録
            // Subscribe before any tick since destruction can happen on the first tick
            var removalReasons = new List<BlockRemoveReason>();
            using var subscription = ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(update => removalReasons.Add(update.RemoveReason));

            // 破壊されるまでティックを進める
            // Advance ticks until destroyed
            for (var i = 0; i < 120 && world.Exists(conveyorPos); i++)
            {
                GameUpdater.UpdateOneTick();
            }

            Assert.IsFalse(world.Exists(conveyorPos));
            Assert.IsTrue(removalReasons.Contains(BlockRemoveReason.Broken));
        }

        [Test]
        public void GearBeltConveyorWithinThresholdRemains()
        {
            // 許容値内では歯車ベルトコンベアが破壊されないことを確認
            // Ensure gear belt conveyor stays when within allowed thresholds
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var conveyorPos = new Vector3Int(0, 0, 0);

            // 過負荷なし（ジェネレータなし）で設置
            // Place without overload (no generator)
            world.TryAddBlock(ForUnitTestModBlockId.SmallGearBeltConveyor, conveyorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 十分なティックを進めても存在し続けることを確認
            // Verify it remains after sufficient ticks
            for (var i = 0; i < 160; i++)
            {
                GameUpdater.UpdateOneTick();
            }

            Assert.IsTrue(world.Exists(conveyorPos));
        }
    }
}
