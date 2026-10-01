using System.Collections.Generic;
using Core.BeltTransport;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;
using NUnit.Framework;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using static Tests.CombinedTest.Core.Transport.Segment.BeltWorldTransportTest;

namespace Tests.CombinedTest.Core.Transport.Segment
{
    public class BeltWorldLifecycleTest : IBeltItemDropObserver
    {
        public void OnDropped(BeltCellItemState item, string reason) => Assert.Fail(reason);

        [Test]
        public void BufferOnlyInventoryKeepsRunningSlotEmptyTest()
        {
            var transport = CreateWorld();
            var belt = Place(ForUnitTestModBlockId.GearBeltConveyorSplitter, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            Place(ForUnitTestModBlockId.ChestId, 0, 1, BlockDirection.North);
            Place(ForUnitTestModBlockId.ChestId, -1, 0, BlockDirection.North);
            transport.Initialize();
            var buffered = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
            belt.SetItem(1, buffered);
            Assert.AreEqual(0, belt.GetItem(0).Count);
            Assert.AreEqual(buffered.ItemInstanceId, belt.GetItem(1).ItemInstanceId);
            belt.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1));
            Assert.AreEqual(2, transport.Network.CaptureItems().Length);
            Assert.AreEqual(buffered.ItemInstanceId, belt.GetItem(1).ItemInstanceId);
            belt.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            Assert.AreEqual(1, transport.Network.CaptureItems().Length);
            Assert.AreEqual(buffered.ItemInstanceId, belt.GetItem(1).ItemInstanceId);
        }

        [Test]
        public void CaptureDuringTickEndReturnsPriorCommittedBoundaryTest()
        {
            var transport = CreateWorld();
            var belt = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            belt.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
            transport.Initialize();
            var initial = transport.CaptureCommittedSnapshot();
            BeltNetworkReplay replay = null;
            transport.OnTickCompleted.Subscribe(delta => replay.Apply(delta));
            GameUpdater.TickEndUpdates.Add(CaptureAndPlace);
            GameUpdater.UpdateOneTick();
            CollectionAssert.AreEqual(transport.Network.CaptureItems(), replay.Network.CaptureItems());
            Assert.AreEqual(transport.CaptureCommittedSnapshot().Tick, replay.Tick);
            Assert.AreEqual(2, transport.CaptureCommittedSnapshot().Snapshot.Cells.Length);

            #region Internal
            void CaptureAndPlace()
            {
                // Drainと同じ位置で取得しても進行途中の状態を返さない。
                // Capture at the same phase as Drain without exposing an in-progress state.
                var captured = transport.CaptureCommittedSnapshot();
                Assert.AreEqual(initial.Tick, captured.Tick);
                CollectionAssert.AreEqual(initial.Snapshot.Items, captured.Snapshot.Items);
                replay = new BeltNetworkReplay(captured.Tick, captured.Snapshot, this);
                Place(ForUnitTestModBlockId.BeltConveyorId, 0, 1, BlockDirection.North);
            }
            #endregion
        }

        [Test]
        public void TopologyRemovalAndSeededPlacementReplayFromDifferencesTest()
        {
            var transport = CreateWorld();
            var first = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var middle = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 1, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var last = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 2, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            first.SetTicksOfItemEnterToExit(uint.MaxValue);
            middle.SetTicksOfItemEnterToExit(uint.MaxValue);
            last.SetTicksOfItemEnterToExit(uint.MaxValue);
            first.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));
            last.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1));
            transport.Initialize();
            var replay = new BeltNetworkReplay(transport.CaptureCommittedSnapshot().Tick, transport.CaptureCommittedSnapshot().Snapshot, this);
            transport.OnTickCompleted.Subscribe(difference => replay.Apply(Tests.Util.BeltTransport.BeltWireRoundTrip.Tick(difference)));
            ulong startTick = GameUpdater.CurrentTick;
            GameUpdater.TickEndUpdates.Add(Mutate);
            for (int tick = 0; tick < 3; tick++)
            {
                GameUpdater.UpdateOneTick();
                CollectionAssert.AreEqual(transport.Network.CaptureItems(), replay.Network.CaptureItems());
            }
            Assert.AreEqual(3, transport.Network.CaptureItems().Length);

            #region Internal
            void Mutate()
            {
                if (GameUpdater.CurrentTick == startTick + 1) ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, 1), BlockRemoveReason.ManualRemove);
                if (GameUpdater.CurrentTick != startTick + 2) return;
                var placed = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 1, BlockDirection.East).GetComponent<VanillaBeltConveyorComponent>();
                placed.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId3, 1));
            }
            #endregion
        }

        [Test]
        public void NewSaveRestoresBranchPriorityAndSeparateBufferItemTest()
        {
            var transport = CreateWorld();
            var branchBlock = Place(ForUnitTestModBlockId.GearBeltConveyorSplitter, 0, 0, BlockDirection.North);
            var branch = branchBlock.GetComponent<VanillaBeltConveyorComponent>();
            Place(ForUnitTestModBlockId.ChestId, 0, 1, BlockDirection.North);
            Place(ForUnitTestModBlockId.ChestId, -1, 0, BlockDirection.North);
            Place(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, 1, 0, BlockDirection.East);
            Place(ForUnitTestModBlockId.SmallGear, 2, 0, BlockDirection.East);
            transport.Initialize();
            branch.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));
            branch.SetItem(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1));
            GameUpdater.UpdateOneTick();
            branch.SetItem(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId3, 1));
            var savedState = (BeltCellSaveState)branch.GetSaveState();
            Assert.AreNotEqual(56, savedState.PriorityOrder);
            Assert.AreEqual(1, savedState.Items.Count);
            Assert.IsNotNull(savedState.BufferItem);
            var expected = transport.Network.CaptureItems();
            var text = JsonConvert.SerializeObject(ServerContext.WorldBlockDatastore.GetSaveJsonObject());

            // 実在庫のバッファと順序を検証。
            // Verify buffer and priority through real inventory components.
            var loadedTransport = CreateWorld();
            ServerContext.WorldBlockDatastore.LoadBlockDataList(JsonConvert.DeserializeObject<List<BlockJsonObject>>(text));
            loadedTransport.Initialize();
            var loaded = ServerContext.WorldBlockDatastore.GetBlock(branchBlock.BlockInstanceId).GetComponent<VanillaBeltConveyorComponent>();
            var actualState = (BeltCellSaveState)loaded.GetSaveState();
            Assert.AreEqual(savedState.PriorityOrder, actualState.PriorityOrder);
            Assert.AreEqual(savedState.BufferItem.InstanceId, actualState.BufferItem.InstanceId);
            CollectionAssert.AreEqual(expected, loadedTransport.Network.CaptureItems());
        }
    }
}
