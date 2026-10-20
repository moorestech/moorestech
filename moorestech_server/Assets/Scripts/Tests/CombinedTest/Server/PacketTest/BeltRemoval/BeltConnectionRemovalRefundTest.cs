using System.Linq;
using Core.BeltTransport;
using Core.Update;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.BeltConnection.Transport;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BeltConnectionRemovalRefundTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void EmptyNonBeltSourceRefundsInternalItem(bool electricMachine)
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            var sourcePosition = new Vector3Int(-1, 0, 1);
            if (electricMachine)
            {
                for (var z = 0; z <= 2; z++) world.Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z));
                world.Place(ForUnitTestModBlockId.MachineId, sourcePosition);
                EnterMachinePushWindow();
            }
            else world.CreateConnectionMerge(true, false);
            var item = world.RestoreInternal(BeltDirection.Left);

            // チェストと電気機械を同じ接続規則で扱う
            // Apply the same connection rule to a chest and an electric machine
            Assert.IsTrue(world.Remove(sourcePosition).Success);
            Assert.AreEqual(1, world.Count(item.ItemId));
            world.AssertRefund(item);
            Assert.AreEqual(0, world.CarriedCount());
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, world.Count(item.ItemId));
            Assert.AreEqual(0, world.CarriedCount());
        }

        [TestCase(false, 2)]
        [TestCase(true, 1)]
        public void RefundsExactlyTheInternalDirectionsThatDisappear(bool beltInput, int refunded)
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            world.CreateConnectionMerge(beltInput, true);
            var left = world.RestoreInternal(BeltDirection.Left);
            var right = world.RestoreInternal(BeltDirection.Right);

            // 2→1は両方向、3→2は撤去側だけ返す
            // Refund both directions for 2-to-1, only the removed input for 3-to-2
            Assert.IsTrue(world.Remove(new Vector3Int(-1, 0, 1)).Success);
            world.AssertRefund(left);
            if (!beltInput) world.AssertRefund(right);
            Assert.AreEqual(refunded, world.Count(left.ItemId));
            Assert.AreEqual(2 - refunded, world.CarriedCount());
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(2, world.Count(left.ItemId) + world.CarriedCount());
            if (beltInput)
                Assert.IsTrue(Assembly().Segments.SelectMany(segment => segment.CaptureItems()).Any(state => state.Item.ItemInstanceId == right.ItemInstanceId));
        }

        [Test]
        public void MultipleOutputMergesRefundEveryAffectedItem()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            BeltTransportTestUtil.InstallMachinePorts(new[] { Vector3Int.left, Vector3Int.right }, new[] { Vector3Int.back });
            foreach (var x in new[] { -1, 1 })
                for (var z = 0; z <= 2; z++) world.Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(x, 0, z));
            world.Place(ForUnitTestModBlockId.ChestId, Vector3Int.forward);
            EnterMachinePushWindow();
            Assert.AreEqual(2, Assembly().Layouts.Count(layout => layout.IsInternal));
            var left = world.RestoreInternal(BeltDirection.Left);
            var right = world.RestoreInternal(BeltDirection.Right);

            // 同一搬出元の全出力を一度ずつ返す
            // Refund every output of the same source exactly once
            Assert.IsTrue(world.Remove(Vector3Int.forward).Success);
            world.AssertRefund(left);
            world.AssertRefund(right);
            Assert.AreEqual(2, world.Count(left.ItemId));
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, world.CarriedCount());
            Assert.AreEqual(2, world.Count(left.ItemId));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SourceAndMergeRemovalInEitherOrderNeverDuplicates(bool mergeFirst)
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            world.CreateConnectionMerge(true, false);
            var item = world.RestoreInternal(BeltDirection.Left);
            var source = new Vector3Int(-1, 0, 1);
            var positions = mergeFirst ? new[] { Vector3Int.forward, source } : new[] { source, Vector3Int.forward };

            // 再構築を挟まず撤去の順序を反転する
            // Reverse removal order without rebuilding between requests
            foreach (var position in positions) Assert.IsTrue(world.Remove(position).Success);
            Assert.AreEqual(1, world.Count(item.ItemId));
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, world.CarriedCount());
            Assert.AreEqual(1, world.Count(item.ItemId));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConsecutiveSourcesUseCurrentInputCount(bool beltInput)
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            world.CreateConnectionMerge(beltInput, true);
            var item = world.RestoreInternal(BeltDirection.Left);
            world.RestoreInternal(BeltDirection.Right);

            // 古い合流入力数に依存せず次の撤去を計算する
            // Calculate the next removal without relying on stale merge input counts
            Assert.IsTrue(world.Remove(new Vector3Int(-1, 0, 1)).Success);
            Assert.IsTrue(world.Remove(new Vector3Int(1, 0, 1)).Success);
            Assert.AreEqual(2, world.Count(item.ItemId));
            Assert.IsTrue(world.Remove(Vector3Int.forward).Success);
            Assert.AreEqual(2, world.Count(item.ItemId));
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, world.CarriedCount());
        }

        [Test]
        public void UnrelatedRemovalPreservesInternalItems()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            world.CreateConnectionMerge(true, false);
            var item = world.RestoreInternal(BeltDirection.Left);
            var position = new Vector3Int(10, 0, 10);
            world.Place(ForUnitTestModBlockId.ChestId, position);
            Assert.IsTrue(world.Remove(position).Success);
            Assert.AreEqual(0, world.Count(item.ItemId));
            Assert.AreEqual(1, world.CarriedCount());
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, world.CarriedCount());
        }
    }
}
