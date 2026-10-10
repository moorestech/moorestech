using System.Linq;
using Core.BeltTransport;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Save.BeltSaveTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Transport.BeltTransportTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Save
{
    // 実blockのセーブ入口が、組からそのblockに属する内容だけを切り出すか
    // Whether the real block's save entry cuts out of the assembly exactly the content belonging to that block
    public class BeltConveyorSaveStateTest
    {
        [Test]
        public void StraightLineSavesEachItemOnTheBlockOfItsHead()
        {
            var world = NewWorld();
            PlaceStraightLine(world, 0);
            Rebuild();

            // segment出口から50・400・700。マスの出口はz=2が0、z=1が256、z=0が512
            // 50, 400 and 700 from the segment exit; cell exits are 0 for z=2, 256 for z=1 and 512 for z=0
            CurrentSegmentAt(Vector3Int.zero).RestoreItems(new[]
            {
                new BeltItemState(NewItem(ItemA, BeltEntryDirection.FromBack), 50),
                new BeltItemState(NewItem(ItemB, BeltEntryDirection.FromBack), 400),
                new BeltItemState(NewItem(ItemC, BeltEntryDirection.FromBack), 700)
            });

            // 各blockは先頭が自マスにある1個だけを、自マス出口までの距離で持つ
            // Each block holds only the one item whose head is on its cell, by the distance to its own cell exit
            AssertOnlyRunningItem(SaveStateAt(world, new Vector3Int(0, 0, 2)), ItemA, 50);
            AssertOnlyRunningItem(SaveStateAt(world, new Vector3Int(0, 0, 1)), ItemB, 144);
            AssertOnlyRunningItem(SaveStateAt(world, new Vector3Int(0, 0, 0)), ItemC, 188);
        }

        [Test]
        public void MergeBlockSavesPriorityOrderAndBufferItem()
        {
            var world = MergeWorld();
            Rebuild();
            RotateMergeOrderByPassingOneItem(NewItem(ItemA, BeltEntryDirection.FromBack));
            var merge = (BeltBufferedSegment)CurrentSegmentAt(MergeCell);
            merge.Buffer.RestoreItem(NewItem(ItemB, BeltEntryDirection.FromRight));

            // 合流blockは回った優先順とbufferのアイテムを持ち、走行中・内部segmentは空
            // The merge block holds the rotated order and the buffer item; running and internal lists are empty
            var mergeState = SaveStateAt(world, MergeCell);
            Assert.AreEqual(merge.PriorityOrder, mergeState.PriorityOrder);
            Assert.AreEqual(RotatedMergeOrder, mergeState.PriorityOrder);
            AssertSavedItem(mergeState.BufferItem, ItemB, BeltEntryDirection.FromRight, 0);
            Assert.IsEmpty(mergeState.Items);
            Assert.IsEmpty(mergeState.InternalItems);

            // 横のベルト(B)は合流でも分岐でもないので優先順・bufferを持たない
            // The side belt (B) is neither a merge nor a branch, so it has no order or buffer
            var sideState = SaveStateAt(world, new Vector3Int(1, 0, 1));
            Assert.AreEqual(BeltPriority.InitializeFromDirection, sideState.PriorityOrder);
            Assert.IsNull(sideState.BufferItem);
            Assert.IsEmpty(sideState.Items);

            // 通り抜けたアイテムはCの出口で止まり、Cのblockが持つ
            // The passed item parks at C's exit and is held by C's block
            AssertOnlyRunningItem(SaveStateAt(world, new Vector3Int(0, 0, 2)), ItemA, 0);
        }

        [Test]
        public void MergeBlockSavesInternalSegmentItemByInputDirection()
        {
            var world = MachineMergeWorld();
            Rebuild();
            var assembly = Datastore().Assembly;
            assembly.Segments[InternalSegmentIndex(assembly)].RestoreItems(new[] { new BeltItemState(NewItem(ItemA, BeltEntryDirection.FromLeft), 100) });

            // 内部segmentのアイテムは合流blockの内部枠に、入力方向(左)と距離100で入る
            // The internal segment's item goes into the merge block's internal list with input direction left and distance 100
            var mergeState = SaveStateAt(world, MergeCell);
            var saved = mergeState.InternalItems.Single();
            Assert.AreEqual((int)BeltDirection.Left, saved.InputDirection);
            AssertSavedItem(saved.Item, ItemA, BeltEntryDirection.FromLeft, 100);
            Assert.AreEqual(InitialMergeOrder, mergeState.PriorityOrder);
            Assert.IsNull(mergeState.BufferItem);

            // 走行中アイテムとしてはどのblockにも現れない
            // It never appears as a running item on any block
            for (var z = 0; z <= 2; z++) Assert.IsEmpty(SaveStateAt(world, new Vector3Int(0, 0, z)).Items, $"running items of z={z}");
        }

        [Test]
        public void BeltPlacedThisTickSavesAsEmpty()
        {
            var world = NewWorld();
            PlaceStraightLine(world, 0);
            Rebuild();
            CurrentSegmentAt(Vector3Int.zero).RestoreItems(new[] { new BeltItemState(NewItem(ItemA, BeltEntryDirection.FromBack), 0) });

            // 再構築前に置いたblockはまだ組に無く、空として保存される
            // A block placed before the rebuild is not in the assembly yet and saves as empty
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 3), BlockDirection.North);
            var state = SaveStateAt(world, new Vector3Int(0, 0, 3));
            Assert.IsEmpty(state.Items);
            Assert.AreEqual(BeltPriority.InitializeFromDirection, state.PriorityOrder);
            Assert.IsNull(state.BufferItem);
            Assert.IsEmpty(state.InternalItems);

            // 既存blockは旧組から切り出され続ける
            // Existing blocks keep being cut out of the old assembly
            AssertOnlyRunningItem(SaveStateAt(world, new Vector3Int(0, 0, 2)), ItemA, 0);
        }

        private static void AssertOnlyRunningItem(BeltConveyorSaveJsonObject state, ItemId itemId, int distanceToCellExit)
        {
            Assert.AreEqual(1, state.Items.Count, "running items on the block");
            AssertSavedItem(state.Items[0], itemId, BeltEntryDirection.FromBack, distanceToCellExit);
            Assert.AreEqual(BeltPriority.InitializeFromDirection, state.PriorityOrder);
            Assert.IsNull(state.BufferItem);
            Assert.IsEmpty(state.InternalItems);
        }
    }
}
