using System.Text.RegularExpressions;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.UnitTest.Game.BeltConnection.Save.BeltSaveTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Transport.BeltTransportTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Save
{
    // 保存した優先順はロード時だけ使われ、読めない値や通常の再構築では向きから初期化される
    // A saved priority order is used only on load; an unreadable value or a normal rebuild initializes it from the direction
    public class BeltConveyorLoadStatePriorityTest
    {
        // 63は3方向とも右、56は搬出方向(前)を含む、37は後が重複、121は6bitより上が立つ、-2は負
        // 63 is right three times, 56 includes the output (front), 37 repeats back, 121 has bits above bit 5, -2 is negative
        [TestCase(63)]
        [TestCase(56)]
        [TestCase(37)]
        [TestCase(121)]
        [TestCase(-2)]
        public void UnreadableMergeOrderIsInitializedFromDirectionAndItemsStay(int corruptedOrder)
        {
            var world = MergeWorld();
            Rebuild();
            ((BeltBufferedSegment)CurrentSegmentAt(MergeCell)).Buffer.RestoreItem(NewItem(ItemB, BeltEntryDirection.FromRight));
            var saved = SaveWorld(world);
            SavedStateAt(saved, MergeCell)["priorityOrder"] = corruptedOrder;

            // 優先順だけが向きからの初期値になり、bufferのアイテムは戻る
            // Only the order falls back to the direction-based value; the buffer item still returns
            NewWorld().LoadBlockDataList(saved);
            LogAssert.Expect(LogType.Error, new Regex("unreadable saved priority order"));
            Rebuild();
            var merge = (BeltBufferedSegment)CurrentSegmentAt(MergeCell);
            Assert.AreEqual(BeltPriority.Create(BeltDirections.Opposite(BeltDirection.Front)), merge.PriorityOrder);
            Assert.IsTrue(merge.Buffer.TryGetItem(out var held));
            Assert.AreEqual(ItemB, held.ItemId);
        }

        [Test]
        public void TopologyRebuildResetsRotatedOrderWhileSaveLoadKeepsIt()
        {
            var world = MergeWorld();
            Rebuild();
            RotateMergeOrderByPassingOneItem(NewItem(ItemA, BeltEntryDirection.FromBack));
            var saved = SaveWorld(world);

            // 無関係なベルコンを置いた再構築では、合流の優先順は向きから作り直される
            // A rebuild from placing an unrelated belt recreates the merge order from the direction
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(10, 0, 10), BlockDirection.North);
            Rebuild();
            Assert.AreEqual(InitialMergeOrder, CurrentSegmentAt(MergeCell).PriorityOrder);

            // セーブ→ロードでは回った優先順のまま
            // A save and load keeps the rotated order
            var loaded = NewWorld();
            loaded.LoadBlockDataList(saved);
            Rebuild();
            Assert.AreEqual(RotatedMergeOrder, CurrentSegmentAt(MergeCell).PriorityOrder);

            // ロード後に置いた無関係なベルコンの再構築では、また向きからの初期値に戻る
            // A later rebuild from an unrelated placement after the load goes back to the direction-based value
            Place(loaded, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(10, 0, 10), BlockDirection.North);
            Rebuild();
            Assert.AreEqual(InitialMergeOrder, CurrentSegmentAt(MergeCell).PriorityOrder);
        }

        [Test]
        public void BranchOrderIsTakenFromItsLastCellOnLoad()
        {
            var world = BranchWorld();
            var splitter = world.GetBlock(Vector3Int.zero);
            Rebuild();
            var initial = BeltPriority.Create(BeltDirection.Front);
            Assert.AreEqual(initial, CurrentSegmentAt(Vector3Int.zero).PriorityOrder);

            // 直進(前)を末尾へ回した順は搬入方向(後)を含まないので受け入れる
            // The order with the straight output (front) moved last excludes the input (back) and is accepted
            var rotated = BeltPriority.MoveLast(initial, (int)BeltDirection.Front);
            Datastore().RegisterLoadedState(splitter.BlockInstanceId, new BeltConveyorSaveJsonObject { PriorityOrder = rotated });
            Rebuild();
            Assert.AreEqual(rotated, CurrentSegmentAt(Vector3Int.zero).PriorityOrder);
            Assert.AreEqual(rotated, SaveStateAt(world, Vector3Int.zero).PriorityOrder);
        }

        [Test]
        public void BranchOrderContainingInputDirectionIsRejected()
        {
            var world = BranchWorld();
            var splitter = world.GetBlock(Vector3Int.zero);
            Rebuild();

            // 後(搬入方向)・左・右の並びは分岐の搬出順として読めない
            // Back (the input), left and right cannot be read as the branch's output order
            var withInput = (int)BeltDirection.Back | ((int)BeltDirection.Left << 2) | ((int)BeltDirection.Right << 4);
            Datastore().RegisterLoadedState(splitter.BlockInstanceId, new BeltConveyorSaveJsonObject { PriorityOrder = withInput });
            LogAssert.Expect(LogType.Error, new Regex("unreadable saved priority order"));
            Rebuild();
            Assert.AreEqual(BeltPriority.Create(BeltDirection.Front), CurrentSegmentAt(Vector3Int.zero).PriorityOrder);
        }

        // 後ろのベルコンから北向き分配器(0,0,0)へ入り、前・左・右のベルコンへ分かれる
        // From the belt behind into a north-facing splitter at (0,0,0), splitting to belts in front, left and right
        private static IWorldBlockDatastore BranchWorld()
        {
            var world = NewWorld();
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, -1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.GearBeltConveyorSplitter, Vector3Int.zero, BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(-1, 0, 0), BlockDirection.West);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 0), BlockDirection.East);
            return world;
        }
    }
}
