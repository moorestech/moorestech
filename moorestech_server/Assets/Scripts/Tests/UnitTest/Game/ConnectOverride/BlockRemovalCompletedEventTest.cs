using System;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.UnitTest.Game.ConnectOverride
{
    public class BlockRemovalCompletedEventTest
    {
        [Test]
        public void CompletionSeesWorldAfterDeletionAndOnlySuccessfulRemoval()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var events = ServerContext.WorldBlockUpdateEvent;
            var position = new Vector3Int(5, 0, 5);
            var pre = 0;
            var completed = 0;
            var globalCompleted = 0;
            using (events.GetBlockRemoveEvent(position).Subscribe(_ =>
                   {
                       Assert.IsTrue(world.Exists(position));
                       pre++;
                   }))
            using (events.GetBlockRemovalCompletedEvent(position).Subscribe(_ =>
                   {
                       Assert.IsFalse(world.Exists(position));
                       completed++;
                   }))
            using (events.OnBlockRemovalCompleted.Subscribe(_ => globalCompleted++))
            {
                Assert.IsFalse(world.RemoveBlock(position, BlockRemoveReason.ManualRemove));
                world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, position,
                    BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
                Assert.IsTrue(world.RemoveBlock(position, BlockRemoveReason.ManualRemove));
            }
            Assert.AreEqual(1, pre);
            Assert.AreEqual(1, completed);
            Assert.AreEqual(1, globalCompleted);
        }

        [Test]
        public void DisposedCoordinateSubscriberDoesNotReceiveCompletion()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var position = new Vector3Int(6, 0, 6);
            var completed = 0;
            var subscription = ServerContext.WorldBlockUpdateEvent
                .GetBlockRemovalCompletedEvent(position).Subscribe(_ => completed++);
            subscription.Dispose();
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, position,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.RemoveBlock(position, BlockRemoveReason.ManualRemove);
            Assert.AreEqual(0, completed);
        }

        [Test]
        public void OccupiedCellReceivesCompletionAfterAllCoordinatesAreDeleted()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var origin = new Vector3Int(20, 0, 20);
            var occupied = origin + Vector3Int.right;
            var received = 0;
            using (ServerContext.WorldBlockUpdateEvent.GetBlockRemovalCompletedEvent(occupied)
                   .Subscribe(_ =>
                   {
                       Assert.IsFalse(world.Exists(origin));
                       Assert.IsFalse(world.Exists(occupied));
                       received++;
                   }))
            {
                Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.MultiBlockGeneratorId,
                    origin, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
                Assert.IsTrue(world.RemoveBlock(origin, BlockRemoveReason.ManualRemove));
            }
            Assert.AreEqual(1, received);
        }
    }
}
