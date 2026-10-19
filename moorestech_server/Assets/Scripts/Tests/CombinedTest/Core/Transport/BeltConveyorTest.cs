using Core.BeltTransport;
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
    // 実際のtick経路でTestBeltConveyor(速度6、1マス=360)の搬送を確かめる
    // Checks TestBeltConveyor transport (speed 6, one cell = 360) through the real tick path
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

            // tick1で進入距離1で入り出口まで3*360-1=1079。速度6で179tick後に残り5、180tick目に渡る
            // Enters at length 1 on tick 1 leaving 3*360-1=1079; at speed 6 it is 5 away after 179 belt ticks and handed over on tick 180
            GameUpdater.RunFrames(1);
            Assert.AreEqual(2, CountOf(source, ItemA), "1回の押し込みで入るのは1個だけ");
            Assert.AreEqual(3 * BeltConstants.ItemWidth - 1 - 6, ItemsOnSegmentAt(Vector3Int.zero)[0].DistanceToExit);
            GameUpdater.RunFrames(178);
            Assert.AreEqual(0, CountOf(output, ItemA));
            Assert.AreEqual(5, ItemsOnSegmentAt(Vector3Int.zero)[0].DistanceToExit);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, CountOf(output, ItemA));
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
            var beltInventory = Inlet(belt);
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
