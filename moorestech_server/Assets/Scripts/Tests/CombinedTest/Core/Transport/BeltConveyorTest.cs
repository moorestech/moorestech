using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Core.Transport
{
    // 実際のtick経路でTestBeltConveyor(速度6、1マス=256)の搬送を確かめる
    // Checks TestBeltConveyor transport (speed 6, one cell = 256) through the real tick path
    public class BeltConveyorTest
    {
        private static readonly ItemId ItemA = new(1);
        private static readonly ItemId ItemB = new(2);

        // 一個のアイテムが入って正しく搬出されるかのテスト
        // One item enters and is output on the exact tick
        [Test]
        public void InsertBeltConveyorTest()
        {
            CreateServer();
            var source = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            PlaceStraightBelts(3);
            var output = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, 3));

            // tick1で進入距離1で入り出口まで3*256-1=767。速度6で127tick後に残り5、128tick目に渡る
            // Enters at length 1 on tick 1 leaving 3*256-1=767; at speed 6 it is 5 away after 127 belt ticks and handed over on tick 128
            GameUpdater.RunFrames(1);
            Assert.AreEqual(2, CountOf(source, ItemA), "1回の押し込みで入るのは1個だけ");
            Assert.AreEqual(761, ItemsOnSegmentAt(Vector3Int.zero)[0].DistanceToExit);
            GameUpdater.RunFrames(126);
            Assert.AreEqual(0, CountOf(output, ItemA));
            Assert.AreEqual(5, ItemsOnSegmentAt(Vector3Int.zero)[0].DistanceToExit);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, CountOf(output, ItemA));
        }

        // 出口が詰まって満杯になったベルトが、出口が空くと次のtickで1個を渡すテスト
        // A belt filled up behind a blocked exit hands one item over on the tick after the exit frees
        [Test]
        public void FullBeltReleasesHeadWhenOutputFreesTest()
        {
            CreateServer();
            var source = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            PlaceStraightBelts(3);
            var output = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North));
            for (var i = 0; i < output.GetSlotSize(); i++) output.SetItem(i, ServerContext.ItemStackFactory.Create(ItemB, 1));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, 10));

            // 3マスのsegmentには3個までしか載らない。出口で詰まると0・256・512に密着して止まり、それ以上は入らない
            // A three-cell segment holds at most three items; blocked at the exit they pack at 0, 256 and 512 and nothing more enters
            GameUpdater.RunFrames(600);
            var parked = ItemsOnSegmentAt(Vector3Int.zero);
            CollectionAssert.AreEqual(new[] { 0, 256, 512 }, new[] { parked[0].DistanceToExit, parked[1].DistanceToExit, parked[2].DistanceToExit });
            Assert.AreEqual(3, parked.Length);
            Assert.AreEqual(7, CountOf(source, ItemA));
            Assert.AreEqual(0, CountOf(output, ItemA));

            // 1スロット空けると次のtickで先頭が渡り、残りは速度6だけ進む
            // Freeing one slot hands the head over on the next tick and the rest advance by the speed 6
            output.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, CountOf(output, ItemA));
            var moved = ItemsOnSegmentAt(Vector3Int.zero);
            CollectionAssert.AreEqual(new[] { 250, 506 }, new[] { moved[0].DistanceToExit, moved[1].DistanceToExit });
        }

        // 1回の押し込みで1個だけ入り、満杯のsegmentへの押し込みは差し戻されるテスト
        // One push enters exactly one item, and a push into a full segment is returned unchanged
        [Test]
        public void InsertIntoFullSegmentIsRejectedTest()
        {
            CreateServer();
            var source = Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North);
            var belt = Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North);
            ServerContext.GetService<BeltTransportDatastore>().RebuildIfDirty();
            var beltInventory = Inventory(belt);
            var context = new InsertItemContext(source.BlockInstanceId, null, null);

            // 1マスのsegmentは1個で満杯。1回目は1個減って返り、2回目はそのまま返る
            // A one-cell segment is full with one item; the first push returns one fewer, the second returns unchanged
            var first = ServerContext.ItemStackFactory.Create(ItemA, 5);
            var second = ServerContext.ItemStackFactory.Create(ItemB, 3);
            Assert.AreEqual(ServerContext.ItemStackFactory.Create(ItemA, 4), beltInventory.InsertItem(first, context));
            Assert.AreEqual(second, beltInventory.InsertItem(second, context));
            Assert.AreEqual(1, ItemsOnSegmentAt(Vector3Int.zero).Length);
            Assert.AreEqual(ItemA, ItemsOnSegmentAt(Vector3Int.zero)[0].Item.ItemId);
        }

        // 歯車ベルトコンベアスプリッタが2方向に分配できるかのテスト
        // The gear belt splitter distributes evenly to its two outputs
        [Test]
        public void GearBeltConveyorSplitterDistributesToTwoChestsTest()
        {
            CreateServer();

            // スプリッター本体とチェストを配置する。搬送速度はマスタ固定なので歯車の動力は要らない
            // Place the splitter and chests; transport speed is fixed by the master so no gear power is needed
            Place(ForUnitTestModBlockId.GearBeltConveyorSplitter, Vector3Int.zero, BlockDirection.North);
            var source = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            var outputA = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 1), BlockDirection.North));
            var outputB = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, 0), BlockDirection.North));
            const int itemCount = 40;
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, itemCount));

            // 分岐は搬出のたびに方向を入れ替えるので、全数を流し切ると半数ずつになる
            // The branch rotates its output direction on every handoff, so draining everything splits it in half
            for (var tick = 0; tick < 2000 && CountOf(outputA, ItemA) + CountOf(outputB, ItemA) < itemCount; tick++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(itemCount / 2, CountOf(outputA, ItemA));
            Assert.AreEqual(itemCount / 2, CountOf(outputB, ItemA));
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static void PlaceStraightBelts(int length)
        {
            for (var z = 0; z < length; z++) Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
        }
    }
}
