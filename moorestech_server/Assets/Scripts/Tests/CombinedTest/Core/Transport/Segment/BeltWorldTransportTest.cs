using System;
using System.Collections.Generic;
using Core.BeltTransport;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core.Transport.Segment
{
    public class BeltWorldTransportTest : IBeltItemDropObserver
    {
        public void OnDropped(BeltCellItemState item, string reason) => Assert.Fail(reason);

        [Test]
        public void MachineNormalMachineTransfersAndReplaysTest()
        {
            var transport = CreateWorld();
            var source = Place(ForUnitTestModBlockId.ChestId, 0, 0, BlockDirection.North).GetComponent<IBlockInventory>();
            var first = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 1, BlockDirection.North);
            var second = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 2, BlockDirection.North);
            var target = Place(ForUnitTestModBlockId.ChestId, 0, 3, BlockDirection.North).GetComponent<IBlockInventory>();
            transport.Initialize();
            var replay = new BeltNetworkReplay(transport.CompletedTick, transport.CaptureSnapshot(), this);
            transport.OnTickCompleted.Subscribe(replay.Apply);
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 3));

            // 逐次機械入力と段階4搬出を、初回状態と差分だけで再現する。
            // Replay serial machine input and stage-four output from initial state and differences only.
            for (int tick = 0; tick < 250; tick++)
            {
                GameUpdater.UpdateOneTick();
                CollectionAssert.AreEqual(transport.Network.CaptureItems(), replay.Network.CaptureItems());
            }
            Assert.AreSame(transport.Network.GetPath(first.BlockInstanceId.AsPrimitive()), transport.Network.GetPath(second.BlockInstanceId.AsPrimitive()));
            Assert.AreEqual(3, Count(target));
            Assert.AreEqual(0, Count(source));
        }

        [Test]
        public void MachineNormalMergeInputUsesReservationTest()
        {
            var transport = CreateWorld();
            var source = Place(ForUnitTestModBlockId.ChestId, 0, -1, BlockDirection.North).GetComponent<IBlockInventory>();
            Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North);
            var merge = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 1, BlockDirection.North);
            var side = Place(ForUnitTestModBlockId.BeltConveyorId, -1, 1, BlockDirection.East).GetComponent<VanillaBeltConveyorComponent>();
            var target = Place(ForUnitTestModBlockId.ChestId, 0, 2, BlockDirection.North).GetComponent<IBlockInventory>();
            side.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1));
            transport.Initialize();
            Assert.AreEqual(BeltSegmentKind.Merge, transport.Network.GetPath(merge.BlockInstanceId.AsPrimitive()).Segment.Kind);
            var replay = new BeltNetworkReplay(transport.CompletedTick, transport.CaptureSnapshot(), this);
            transport.OnTickCompleted.Subscribe(replay.Apply);
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 2));
            for (int tick = 0; tick < 250; tick++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(3, Count(target));
            CollectionAssert.AreEqual(transport.Network.CaptureItems(), replay.Network.CaptureItems());
        }

        [Test]
        public void RemovingHeadCellDropsOnlyItsItemAtTickBoundaryTest()
        {
            var transport = CreateWorld();
            var first = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var second = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 1, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            first.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));
            second.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1));
            transport.Initialize();
            GameUpdater.TickEndUpdates.Add(Remove);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, transport.Network.CaptureItems().Length);
            Assert.AreEqual(first.CellId, transport.Network.CaptureItems()[0].CellId);
            Assert.AreEqual(256, transport.Network.CaptureItems()[0].Progress);

            #region Internal
            void Remove() => ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, 1), BlockRemoveReason.ManualRemove);
            #endregion
        }

        internal static BeltWorldTransport CreateWorld()
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return services.GetRequiredService<BeltWorldTransport>();
        }
        internal static IBlock Place(BlockId id, int x, int z, BlockDirection direction)
        {
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(id, new Vector3Int(x, 0, z), direction, Array.Empty<BlockCreateParam>(), out var block));
            return block;
        }
        private static int Count(IBlockInventory inventory)
        {
            int total = 0;
            for (int slot = 0; slot < inventory.GetSlotSize(); slot++) total += inventory.GetItem(slot).Count;
            return total;
        }
    }
}
