using System;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
namespace Tests.CombinedTest.Core
{
    public class BlockSystemTickUpdateTest
    {
        [Test]
        public void CentralTickDrivesSegmentWithoutLegacyPerBlockPhysicsTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var belt);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.forward, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest);
            var facade = belt.GetComponent<VanillaBeltConveyorComponent>();
            facade.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
            var inventory = chest.GetComponent<IBlockInventory>();
            // 単体block tickでは二重進行せず、中央tickだけがsegmentを進める。
            // Per-block ticks cannot double-advance transport; only central ticks advance the segment.
            var progress = facade.CaptureItems()[0].Progress;
            for (int tick = 0; tick < 100; tick++) belt.TickUpdate();
            Assert.AreEqual(progress, facade.CaptureItems()[0].Progress);
            Assert.AreEqual(0, inventory.GetItem(0).Count);
            for (int tick = 0; tick < 100 && inventory.GetItem(0).Count == 0; tick++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, inventory.GetItem(0).Count);
        }
    }
}
