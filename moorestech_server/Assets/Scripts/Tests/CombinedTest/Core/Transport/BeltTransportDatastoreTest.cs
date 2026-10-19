using System.Text.RegularExpressions;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Core.Transport
{
    // ベルト搬送の組を持つDatastoreの、再構築の契機と押し込み受付区間を実tick経路で確かめる
    // Checks, through the real tick path, the belt transport datastore's rebuild triggers and its push-acceptance window
    public class BeltTransportDatastoreTest
    {
        private static readonly ItemId ItemA = new(1);
        private static readonly Vector3Int MergeCell = new(0, 0, 1);

        // ベルトとも搬入出とも無関係なブロックを置いても組は作り直されず、合流の回った優先順もハッシュも変わらない
        // Placing a block unrelated to belts and item exchange never rebuilds the assembly, so the rotated merge order and the hash stay
        [Test]
        public void UnrelatedBlockPlacementKeepsAssemblyAndHash()
        {
            CreateMergeWorld();
            var datastore = ServerContext.GetService<BeltTransportDatastore>();
            var initialOrder = BeltPriority.Create(BeltDirection.Back);
            var rotatedOrder = BeltPriority.MoveLast(initialOrder, (int)BeltDirection.Back);

            // 直進入力から1個流し切って合流の優先順を回す。アイテムは合流先の出口で止まる
            // Run one item through the straight input to rotate the merge order; it parks at the exit past the merge
            SegmentAt(Vector3Int.zero).RestoreItems(new[] { new BeltItemState(new BeltItem(ItemA, ItemInstanceId.Create(), BeltEntryDirection.FromBack), 0) });
            GameUpdater.RunFrames(200);
            Assert.AreEqual(rotatedOrder, SegmentAt(MergeCell).PriorityOrder, "merge order rotated");
            var assembly = datastore.Assembly;
            var hash = datastore.ComputeStateHash();

            // 電柱はベルトでもインベントリ接続も持たない
            // An electric pole is neither a belt nor has an inventory connector
            var pole = Place(ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(10, 0, 10), BlockDirection.North);
            Assert.IsFalse(pole.ComponentManager.ExistsComponent<IBlockConnectorComponent<IBlockInventory>>(), "precondition: no inventory connector");
            Assert.IsFalse(TickAndReport().WasRebuilt, "no rebuild for an unrelated block");
            Assert.AreSame(assembly, datastore.Assembly, "assembly kept");
            Assert.AreEqual(hash, datastore.ComputeStateHash(), "hash unchanged");
            Assert.AreEqual(rotatedOrder, SegmentAt(MergeCell).PriorityOrder, "merge order kept");

            // 搬入出できるブロック(チェスト)は離れていても契機になり、仕様どおり優先順は初期化される
            // A block that exchanges items (a chest) triggers a rebuild even far away, and the order resets per the spec
            Place(ForUnitTestModBlockId.ChestId, new Vector3Int(20, 0, 20), BlockDirection.North);
            Assert.IsTrue(TickAndReport().WasRebuilt, "a chest triggers a rebuild");
            Assert.AreEqual(initialOrder, SegmentAt(MergeCell).PriorityOrder, "merge order reset by the rebuild");
        }

        // 搬送tick後〜次tick先頭の再構築前の押し込みは、エラーを残して拒否される。再構築の後は受け付ける
        // A push between the transport tick and the next tick-head rebuild is refused with an error; after the rebuild it is accepted
        [Test]
        public void PushAfterTransportTickIsRefusedUntilNextRebuild()
        {
            CreateServer();
            var machine = Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North);
            var inlet = Inlet(Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North));
            GameUpdater.UpdateOneTick();
            var context = new InsertItemContext(machine.BlockInstanceId, null, null);

            // tickを終えた直後は搬送後の区間にいる
            // Right after a tick we are in the post-transport window
            LogAssert.Expect(LogType.Error, new Regex(@"\[BeltTransport\] push into belt block .* arrived after the transport tick"));
            var refused = inlet.InsertItem(ServerContext.ItemStackFactory.Create(ItemA, 1), context);
            Assert.AreEqual(1, refused.Count, "the push is refused");
            Assert.IsEmpty(ItemsOnSegmentAt(Vector3Int.zero), "nothing entered the belt");

            // tick先頭の再構築(dirtyでなくても呼ばれる)で受付区間に戻る
            // The tick-head rebuild (called even when not dirty) reopens the window
            EnterMachinePushWindow();
            var accepted = inlet.InsertItem(ServerContext.ItemStackFactory.Create(ItemA, 1), context);
            Assert.AreEqual(0, accepted.Count, "the push is accepted");
            Assert.AreEqual(1, ItemsOnSegmentAt(Vector3Int.zero).Length, "the item entered the belt");
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        // 直進(z=-1..0)と横(x=1から西向き)が北向きの合流(z=1)に入り、z=2へ流れる
        // The straight line (z=-1..0) and the side belt (westward from x=1) enter the north-facing merge (z=1), which flows to z=2
        private static void CreateMergeWorld()
        {
            CreateServer();
            for (var z = -1; z <= 2; z++) Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.West);
            GameUpdater.UpdateOneTick();
        }

        private static BeltTransportTickReport TickAndReport()
        {
            var report = default(BeltTransportTickReport);
            using (ServerContext.GetService<BeltTransportTickUpdater>().OnTransportTickCompleted.Subscribe(completed => report = completed)) GameUpdater.UpdateOneTick();
            return report;
        }
    }
}
