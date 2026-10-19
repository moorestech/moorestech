using Core.Master;
using Core.Update;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Game
{
    public class BeltConveyorInsertTest
    {
        // 2つのアイテムがチェストから出されてベルトコンベアに入り、全てチェストに入るテスト
        // Two items leave a chest, ride one belt cell and both land in the output chest on exact ticks
        [Test]
        public void TwoItemIoTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemId = new ItemId(1);

            // 搬入チェスト→ベルト1マス→搬出チェストを北向きに並べる
            // Line up input chest -> one belt cell -> output chest facing north
            var inputChest = Inventory(Place(ForUnitTestModBlockId.ChestId, Vector3Int.zero, BlockDirection.North));
            Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            var outputChest = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 2), BlockDirection.North));
            inputChest.SetItem(0, global::Game.Context.ServerContext.ItemStackFactory.Create(itemId, 2));

            // 1個目: tick1のblock更新で進入距離1で入り出口まで359。速度6で59tick後に残り5、60tick目に渡る
            // First item: enters at length 1 during tick 1 (359 to the exit); at speed 6 it is 5 away after 59 belt ticks and is handed over on tick 60
            GameUpdater.RunFrames(59);
            Assert.AreEqual(0, CountOf(outputChest, itemId));
            Assert.AreEqual(1, CountOf(inputChest, itemId), "1マスのsegmentは1個で満杯なので2個目はまだ入らない");
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, CountOf(outputChest, itemId));

            // 2個目: 空いたtick61に入り、同じく60tick目(=tick120)に渡る
            // Second item: enters on tick 61 once the cell is free and is likewise handed over on its 60th belt tick (tick 120)
            GameUpdater.RunFrames(1);
            Assert.AreEqual(0, CountOf(inputChest, itemId));
            GameUpdater.RunFrames(58);
            Assert.AreEqual(1, CountOf(outputChest, itemId));
            GameUpdater.RunFrames(1);
            Assert.AreEqual(2, CountOf(outputChest, itemId));
        }
    }
}
