using System.Collections.Generic;
using System.Linq;
using Core.BeltTransport;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Transport.BeltTransportTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Save
{
    internal static class BeltSaveTestUtil
    {
        internal static readonly ItemId ItemA = new(1);
        internal static readonly ItemId ItemB = new(2);
        internal static readonly ItemId ItemC = new(3);
        internal static readonly Vector3Int MergeCell = new(0, 0, 1);
        internal static readonly string SaveKey = typeof(BeltConveyorSaveStateComponent).FullName;

        // 北向き合流の初期順(直進=後が先頭)と、後から1個受け入れて後を末尾へ回した順
        // The initial order of a north-facing merge (straight = back first) and the order after one item from the back moved it last
        internal static readonly int InitialMergeOrder = BeltPriority.Create(BeltDirection.Back);
        internal static readonly int RotatedMergeOrder = BeltPriority.MoveLast(InitialMergeOrder, (int)BeltDirection.Back);

        internal static BeltTransportDatastore Datastore()
        {
            return ServerContext.GetService<BeltTransportDatastore>();
        }

        internal static void Rebuild()
        {
            Datastore().RebuildIfDirty();
        }

        internal static BeltConveyorSegment CurrentSegmentAt(Vector3Int position)
        {
            return SegmentAt(Datastore().Assembly, position);
        }

        // 実blockのセーブ入口から保存内容を得る
        // Get the save content through the real block's save entry
        internal static BeltConveyorSaveJsonObject SaveStateAt(IWorldBlockDatastore world, Vector3Int position)
        {
            return (BeltConveyorSaveJsonObject)world.GetBlock<BeltConveyorSaveStateComponent>(position).GetSaveState();
        }

        // ワールドの保存一覧を、実ロード経路と同じくJSON(JToken)を一度通した形で返す
        // Return the world's save list after passing each state through JSON (JToken), as the real load path does
        internal static List<BlockJsonObject> SaveWorld(IWorldBlockDatastore world)
        {
            return world.GetSaveJsonObject()
                .Select(b => new BlockJsonObject(b.Pos, b.BlockGuidStr, b.InstanceId, SaveLoadJsonTestHelper.ThroughJson(b.ComponentStates), b.Direction))
                .ToList();
        }

        internal static JObject SavedStateAt(List<BlockJsonObject> saved, Vector3Int position)
        {
            return (JObject)saved.Single(b => b.Pos == position).ComponentStates[SaveKey];
        }

        internal static void AssertSavedItem(BeltItemSaveJsonObject saved, ItemId itemId, BeltEntryDirection entryDirection, int distanceToExit)
        {
            Assert.IsNotNull(saved, "saved item");
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemGuid(itemId).ToString(), saved.ItemGuidStr, "item guid");
            Assert.AreEqual((int)entryDirection, saved.EntryDirection, "entry direction");
            Assert.AreEqual(distanceToExit, saved.DistanceToExit, "distance to exit");
        }

        internal static void AssertRestored(BeltItemState actual, ItemId itemId, BeltEntryDirection entryDirection, int distanceToExit)
        {
            Assert.AreEqual(itemId, actual.Item.ItemId, "item id");
            Assert.AreEqual(entryDirection, actual.Item.EntryDirection, "entry direction");
            Assert.AreEqual(distanceToExit, actual.DistanceToExit, "distance to exit");
        }

        // x列に北向き3マス(z=0..2)の直線。両端に何も無いので1本のsegment
        // A north-facing three-cell line (z=0..2) at column x; nothing at either end, so one segment
        internal static void PlaceStraightLine(IWorldBlockDatastore world, int x)
        {
            for (var z = 0; z <= 2; z++) Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(x, 0, z), BlockDirection.North);
        }

        // A(z=-1..0)とB(x=1から西向き)が合流M(z=1)に入り、C(z=2)へ流れる
        // A (z=-1..0) and B (westward from x=1) enter merge M (z=1), which flows to C (z=2)
        internal static IWorldBlockDatastore MergeWorld()
        {
            var world = NewWorld();
            for (var z = -1; z <= 2; z++) Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.West);
            return world;
        }

        // z=0..2 の直線の z=1 へ、左(-X)の機械が内部segment経由で合流する
        // A machine on the left (-X) joins z=1 of the z=0..2 line through an internal segment
        internal static IWorldBlockDatastore MachineMergeWorld()
        {
            var world = NewWorld();
            InstallMachineMergePorts();
            PlaceStraightLine(world, 0);
            Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, 1), BlockDirection.North);
            return world;
        }

        // ロード先ワールドでも同じ機械ポートを登録し直す必要がある
        // The load-side world must register the same machine ports again
        internal static void InstallMachineMergePorts()
        {
            InstallMachinePorts(new[] { Vector3Int.right }, new[] { Vector3Int.back });
        }

        // 合流の直進入力(z=0)の出口に置いたアイテムを流し切り、合流の優先順を回す。アイテムはz=2の出口で止まる
        // Run an item from the exit of the merge's straight input (z=0) all the way through to rotate the merge order; it parks at z=2's exit
        internal static void RotateMergeOrderByPassingOneItem(BeltItem item)
        {
            CurrentSegmentAt(Vector3Int.zero).RestoreItems(new[] { new BeltItemState(item, 0) });
            Tick(Datastore().Assembly, 200);
            Assert.AreEqual(RotatedMergeOrder, CurrentSegmentAt(MergeCell).PriorityOrder, "merge order after one item from the back");
            Assert.AreNotEqual(InitialMergeOrder, RotatedMergeOrder);
            Assert.IsFalse(((BeltBufferedSegment)CurrentSegmentAt(MergeCell)).Buffer.HasItem, "merge buffer drained");
        }
    }
}
