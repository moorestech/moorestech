using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Core
{
    public class ChestLogicTest
    {
        // ベルトコンベアからチェストへアイテムを搬入する
        // A belt conveyor delivers an item into a chest
        [Test]
        public void BeltConveyorInsertChestLogicTest()
        {
            CreateServer();
            var item = ServerContext.ItemStackFactory.Create(new ItemId(3), 1);

            // 搬入元チェスト→ベルト1マス→対象チェスト
            // Source chest -> one belt cell -> target chest
            var source = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North);
            var chest = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 1), BlockDirection.North));
            source.SetItem(0, item);

            // 1マス・速度6: 進入距離1で出口まで255。42tick後に残り3、43tick目にチェストのスロット0へ入る
            // One cell at speed 6: 255 to the exit; 3 away after 42 ticks, lands in chest slot 0 on tick 43
            GameUpdater.RunFrames(42);
            Assert.AreEqual(ItemMaster.EmptyItemId, chest.GetItem(0).Id);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(item.Id, chest.GetItem(0).Id);
            Assert.AreEqual(1, chest.GetItem(0).Count);
        }

        // チェストが隣のベルトコンベアへアイテムを搬出する
        // A chest outputs an item onto the adjacent belt conveyor
        [Test]
        public void BeltConveyorOutputChestLogicTest()
        {
            CreateServer();
            var chest = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North);
            chest.SetItem(0, ServerContext.ItemStackFactory.Create(new ItemId(1), 1));

            // 1tickのblock更新でベルトへ入り、同じtickのベルト更新で255から6進んで249になる
            // It enters the belt during the tick's block update and advances 6 from 255 to 249 in the same tick's belt update
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, chest.GetItem(0).Count);
            var onBelt = ItemsOnSegmentAt(Vector3Int.zero);
            Assert.AreEqual(1, onBelt.Length);
            Assert.AreEqual(new ItemId(1), onBelt[0].Item.ItemId);
            Assert.AreEqual(249, onBelt[0].DistanceToExit);
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
