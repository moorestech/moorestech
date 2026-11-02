using System.Linq;
using Core.BeltTransport;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.SpaceElevator;
using Game.SpaceElevator.Save;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class SpaceElevatorRemovalCapacityTest
    {
        [Test]
        public void ReservedRemovalChecksCapacityAndRefundsOnlyOnceAfterRetry()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            var position = new Vector3Int(20, 0, 0);
            var block = world.Place(ForUnitTestModBlockId.TestSpaceElevatorBase, position);
            var pool = ServerContext.GetService<SpaceElevatorCablePool>();
            var connections = ServerContext.GetService<SpaceElevatorConnectionService>();
            var removals = ServerContext.GetService<IBlockRemovalReservationService>();
            pool.Restore(new SpaceElevatorCablePoolSaveData { CableCount = 100 });
            Assert.AreEqual(global::Game.Block.Interface.Component.SpaceElevatorConnectResult.Connected, connections.Connect(block));
            pool.Restore(new SpaceElevatorCablePoolSaveData { CableCount = int.MaxValue });
            removals.ReserveRemoval(block.BlockInstanceId, BlockRemoveReason.ManualRemove);
            removals.ApplyReservedRemovals();
            Assert.AreSame(block, ServerContext.WorldBlockDatastore.GetBlock(position));
            Assert.AreEqual(int.MaxValue, pool.Amount);

            // 空きができた後の再予約だけが撤去と一度の返却を行う
            // Only a new reservation after capacity frees removes and refunds once
            var other = world.Place(ForUnitTestModBlockId.TestSpaceElevatorBase, new Vector3Int(30, 0, 0));
            connections.Connect(other);
            removals.ReserveRemoval(block.BlockInstanceId, BlockRemoveReason.ManualRemove);
            removals.ApplyReservedRemovals();
            Assert.IsNull(ServerContext.WorldBlockDatastore.GetBlock(position));
            Assert.AreEqual(int.MaxValue, pool.Amount);
            removals.ApplyReservedRemovals();
            Assert.AreEqual(int.MaxValue, pool.Amount);
        }

        [Test]
        public void PoolOverflowRejectsBeforeDiscardingMergeItemsAndRetryRefundsOnce()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            Tests.UnitTest.Game.BeltConnection.Transport.BeltTransportTestUtil.InstallMachinePorts(new[] { Vector3Int.left, Vector3Int.right }, new[] { Vector3Int.back });
            for (var z = 0; z <= 2; z++) world.Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z));
            var position = new Vector3Int(-1, 0, 1);
            var block = world.Place(ForUnitTestModBlockId.TestSpaceElevatorBase, position);
            var pool = ServerContext.GetService<SpaceElevatorCablePool>();
            var connections = ServerContext.GetService<SpaceElevatorConnectionService>();
            pool.Restore(new SpaceElevatorCablePoolSaveData { CableCount = 100 });
            connections.Connect(block);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 2);
            EnterMachinePushWindow();
            var item = world.RestoreInternal(BeltDirection.Left);
            pool.Restore(new SpaceElevatorCablePoolSaveData { CableCount = int.MaxValue });
            var stateHash = world.Transport.ComputeStateHash();
            var before = inventory.InventoryItems.ToArray();
            var player = world.Inventory.InventoryItems.ToArray();

            // 受信プロトコルの拒否でsegment・在庫・ブロックを全て保つ
            // Receiving-protocol rejection preserves segments, inventories and the block
            var denied = world.Remove(position);
            Assert.IsFalse(denied.Success);
            Assert.AreEqual(RemoveBlockProtocol.RemoveBlockFailureReason.CablePoolFull, denied.FailureReason);
            Assert.AreSame(block, ServerContext.WorldBlockDatastore.GetBlock(position));
            Assert.AreEqual(stateHash, world.Transport.ComputeStateHash());
            CollectionAssert.AreEqual(before, inventory.InventoryItems);
            CollectionAssert.AreEqual(player, world.Inventory.InventoryItems);
            Assert.AreEqual(int.MaxValue, pool.Amount);

            // 別拠点の接続で空きができれば同じ要求を安全に再試行できる
            // Connecting another base frees capacity and makes the same request safe to retry
            var other = world.Place(ForUnitTestModBlockId.TestSpaceElevatorBase, new Vector3Int(20, 0, 0));
            connections.Connect(other);
            Assert.IsTrue(world.Remove(position).Success);
            Assert.AreEqual(int.MaxValue, pool.Amount);
            Assert.AreEqual(1, world.Count(item.ItemId));
            Assert.AreEqual(2, world.Count(ForUnitTestItemId.TestElevatorSatellite));
            Assert.AreEqual(0, world.CarriedCount());
        }
    }
}
