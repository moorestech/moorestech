using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Core.Transport
{
    // 実際のtick経路で、撤去・設置による再構築の後もアイテムが所属マスとマス内進行量を保って届くか
    // Through the real tick path, an item keeps its cell and in-cell progress across a rebuild caused by removal and placement and still arrives
    public class BeltConveyorRebuildTest
    {
        private static readonly ItemId ItemA = new(1);

        [Test]
        public void ExtendingBeltMidwayKeepsItemProgressAndArrivalTick()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var source = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            for (var z = 0; z < 3; z++) Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North);
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, 1));

            // tick1で出口まで761、以後毎tick6進むので50tick後は767-300=467。z=1のマスで残り211
            // 761 to the exit after tick 1, then 6 per tick, so 767-300=467 after 50 ticks: on cell z=1 with 211 left in the cell
            GameUpdater.RunFrames(50);
            var before = ItemsOnSegmentAt(Vector3Int.zero);
            Assert.AreEqual(1, before.Length);
            Assert.AreEqual(467, before[0].DistanceToExit);

            // 出口のチェストを外してベルトを1マス延ばし、その先にチェストを置く
            // Remove the output chest, extend the belt by one cell and put the chest beyond it
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, 3), BlockRemoveReason.ManualRemove);
            Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 3), BlockDirection.North);
            var output = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 4), BlockDirection.North));

            // 次のtick先頭で4マスへ作り直され、z=1は出口から512+211=723。同じtickで6進み717
            // The next tick head rebuilds into four cells, z=1 sitting at 512+211=723 from the exit; the same tick advances it to 717
            GameUpdater.RunFrames(1);
            var after = ItemsOnSegmentAt(Vector3Int.zero);
            Assert.AreEqual(1, after.Length);
            Assert.AreEqual(4, Assembly().Layouts[0].Cells.Length);
            Assert.AreEqual(717, after[0].DistanceToExit);
            Assert.AreEqual(before[0].Item.ItemInstanceId, after[0].Item.ItemInstanceId);

            // 717=6*119+3なので119tick後に残り3、次のtickでチェストへ渡る
            // 717=6*119+3, so it is 3 away after 119 ticks and handed to the chest on the next tick
            GameUpdater.RunFrames(119);
            Assert.AreEqual(0, CountOf(output, ItemA));
            Assert.AreEqual(3, ItemsOnSegmentAt(Vector3Int.zero)[0].DistanceToExit);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, CountOf(output, ItemA));
            Assert.IsEmpty(ItemsOnSegmentAt(Vector3Int.zero));
        }

        [Test]
        public void RemovingTrailingBeltDeletesItemsOnItAndKeepsTheRest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var source = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            for (var z = 0; z < 3; z++) Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, 2));

            // 出口が無いので先頭は出口で止まる。2個は0と256に密着して止まり、z=2とz=1のマスに1個ずつ
            // With no output the head parks at the exit; the two items pack at 0 and 256, one on cell z=2 and one on z=1
            GameUpdater.RunFrames(300);
            var before = ItemsOnSegmentAt(Vector3Int.zero);
            CollectionAssert.AreEqual(new[] { 0, 256 }, new[] { before[0].DistanceToExit, before[1].DistanceToExit });

            // z=2を撤去すると、z=2上の先頭は消え、z=1上のアイテムはマス内残り0のまま新しい出口に来る
            // Removing z=2 deletes the head on it, and the z=1 item lands at the new exit with 0 left in its cell
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, 2), BlockRemoveReason.ManualRemove);
            GameUpdater.RunFrames(1);
            var after = ItemsOnSegmentAt(Vector3Int.zero);
            Assert.AreEqual(1, after.Length);
            Assert.AreEqual(0, after[0].DistanceToExit);
            Assert.AreEqual(before[1].Item.ItemInstanceId, after[0].Item.ItemInstanceId);
        }
    }
}
