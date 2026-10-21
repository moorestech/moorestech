using System.Text.RegularExpressions;
using Core.BeltTransport;
using Core.Master;
using Game.Block.Interface;
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
    // 保存内容をJSON経由でロードし、最初の再構築でアイテムが同じ位置へ戻るか。壊れた内容はsegmentごと復元しない
    // Whether loaded content (through JSON) puts items back at the same place on the first rebuild; corrupted content restores nothing of its segment
    public class BeltConveyorLoadStateTest
    {
        [Test]
        public void StraightLineRoundTripKeepsItemsWithNewInstances()
        {
            var world = NewWorld();
            PlaceStraightLine(world, 0);
            Rebuild();
            var originals = new[]
            {
                new BeltItemState(NewItem(ItemA, BeltEntryDirection.FromBack), 50),
                new BeltItemState(NewItem(ItemB, BeltEntryDirection.FromBack), 500),
                new BeltItemState(NewItem(ItemC, BeltEntryDirection.FromBack), 900)
            };
            CurrentSegmentAt(Vector3Int.zero).RestoreItems(originals);
            var saved = SaveWorld(world);

            // 別ワールドへロードし、最初の再構築で同じ距離・種類・進入方向に戻る。個体IDは振り直す
            // Load into another world; the first rebuild restores the same distance, kind and entry direction with reissued instance ids
            NewWorld().LoadBlockDataList(saved);
            Rebuild();
            var restored = CurrentSegmentAt(Vector3Int.zero).CaptureItems();
            Assert.AreEqual(originals.Length, restored.Length);
            for (var i = 0; i < originals.Length; i++)
            {
                AssertRestored(restored[i], originals[i].Item.ItemId, originals[i].Item.EntryDirection, originals[i].DistanceToExit);
                Assert.AreNotEqual(originals[i].Item.ItemInstanceId, restored[i].Item.ItemInstanceId, "instance id is reissued");
            }
        }

        [Test]
        public void MergeRoundTripRestoresOrderBufferAndInternalItem()
        {
            var world = MachineMergeWorld();
            Rebuild();
            RotateMergeOrderByPassingOneItem(NewItem(ItemA, BeltEntryDirection.FromBack));
            var assembly = Datastore().Assembly;
            ((BeltBufferedSegment)CurrentSegmentAt(MergeCell)).Buffer.RestoreItem(NewItem(ItemB, BeltEntryDirection.FromLeft));
            assembly.Segments[InternalSegmentIndex(assembly)].RestoreItems(new[] { new BeltItemState(NewItem(ItemC, BeltEntryDirection.FromLeft), 100) });
            var saved = SaveWorld(world);

            var loaded = NewWorld();
            InstallMachineMergePorts();
            loaded.LoadBlockDataList(saved);
            Rebuild();

            // 優先順は保存値そのまま、bufferと内部segmentのアイテムも同じ内容で戻る
            // The order comes back exactly as saved, and the buffer and internal segment items return with the same content
            var merge = (BeltBufferedSegment)CurrentSegmentAt(MergeCell);
            Assert.AreEqual(RotatedMergeOrder, merge.PriorityOrder);
            Assert.IsTrue(merge.Buffer.TryGetItem(out var held));
            Assert.AreEqual(ItemB, held.ItemId);
            Assert.AreEqual(BeltEntryDirection.FromLeft, held.EntryDirection);
            var next = Datastore().Assembly;
            var internalItems = next.Segments[InternalSegmentIndex(next)].CaptureItems();
            Assert.AreEqual(1, internalItems.Length);
            AssertRestored(internalItems[0], ItemC, BeltEntryDirection.FromLeft, 100);

            // 通り抜けたアイテムはz=2の出口に戻る
            // The passed item returns to z=2's exit
            var tail = CurrentSegmentAt(new Vector3Int(0, 0, 2)).CaptureItems();
            Assert.AreEqual(1, tail.Length);
            AssertRestored(tail[0], ItemA, BeltEntryDirection.FromBack, 0);
        }

        // 要素そのものがnullの保存内容も、壊れたアイテムと同じくsegmentごと落とす
        // A null element in the saved list is dropped segment-wide just like a corrupted item
        [Test]
        public void NullItemElementDropsWholeSegmentButKeepsOtherSegments()
        {
            var world = NewWorld();
            PlaceStraightLine(world, 0);
            PlaceStraightLine(world, 5);
            Rebuild();
            CurrentSegmentAt(Vector3Int.zero).RestoreItems(new[] { new BeltItemState(NewItem(ItemA, BeltEntryDirection.FromBack), 500) });
            CurrentSegmentAt(new Vector3Int(5, 0, 0)).RestoreItems(new[] { new BeltItemState(NewItem(ItemC, BeltEntryDirection.FromBack), 50) });

            var saved = SaveWorld(world);
            SavedStateAt(saved, new Vector3Int(0, 0, 1))["items"][0] = JValue.CreateNull();

            NewWorld().LoadBlockDataList(saved);
            LogAssert.Expect(LogType.Error, new Regex("unreadable saved items"));
            Rebuild();
            Assert.IsEmpty(CurrentSegmentAt(Vector3Int.zero).CaptureItems());
            var other = CurrentSegmentAt(new Vector3Int(5, 0, 0)).CaptureItems();
            Assert.AreEqual(1, other.Length);
            AssertRestored(other[0], ItemC, BeltEntryDirection.FromBack, 50);
        }

        // 欠損マスタの整理が種類を空GUIDにしたアイテムは壊れた値ではない。その1個だけ見送り、同じsegmentの残りはそのまま戻る(エラーログ無し)
        // An item whose kind the missing-master cleanup emptied is not corruption: only it is skipped and the rest of its segment returns as is (no error log)
        [Test]
        public void PrunedItemIsSkippedAloneAndTheRestOfItsSegmentIsRestored()
        {
            var world = NewWorld();
            PlaceStraightLine(world, 0);
            Rebuild();
            CurrentSegmentAt(Vector3Int.zero).RestoreItems(new[]
            {
                new BeltItemState(NewItem(ItemA, BeltEntryDirection.FromBack), 50),
                new BeltItemState(NewItem(ItemB, BeltEntryDirection.FromBack), 500)
            });

            // z=1のアイテム(距離500)の種類を、整理と同じく空GUIDにする
            // Empty the kind of the z=1 item (distance 500) exactly as the cleanup does
            var saved = SaveWorld(world);
            SavedStateAt(saved, new Vector3Int(0, 0, 1))["items"][0]["itemGuid"] = System.Guid.Empty.ToString();

            NewWorld().LoadBlockDataList(saved);
            Rebuild();
            var restored = CurrentSegmentAt(Vector3Int.zero).CaptureItems();
            Assert.AreEqual(1, restored.Length, "only the pruned item is missing");
            AssertRestored(restored[0], ItemA, BeltEntryDirection.FromBack, 50);
        }

        // bufferか内部segmentのアイテムが整理で空にされても、合流blockのもう片方はそのまま戻る
        // When the buffer item or the internal-segment item was emptied by the cleanup, the merge block's other item still returns
        [TestCase("bufferItem", TestName = "PrunedBufferItemIsSkippedAndInternalItemIsRestored")]
        [TestCase("internalItem", TestName = "PrunedInternalItemIsSkippedAndBufferItemIsRestored")]
        public void PrunedMergeItemIsSkippedAlone(string prunedPlace)
        {
            var world = MachineMergeWorld();
            Rebuild();
            var assembly = Datastore().Assembly;
            ((BeltBufferedSegment)CurrentSegmentAt(MergeCell)).Buffer.RestoreItem(NewItem(ItemB, BeltEntryDirection.FromLeft));
            assembly.Segments[InternalSegmentIndex(assembly)].RestoreItems(new[] { new BeltItemState(NewItem(ItemC, BeltEntryDirection.FromLeft), 100) });
            var saved = SaveWorld(world);
            var mergeState = SavedStateAt(saved, MergeCell);
            var target = prunedPlace == "bufferItem" ? mergeState["bufferItem"] : mergeState["internalItems"][0]["item"];
            target["itemGuid"] = System.Guid.Empty.ToString();

            var loaded = NewWorld();
            InstallMachineMergePorts();
            loaded.LoadBlockDataList(saved);
            Rebuild();
            var next = Datastore().Assembly;
            var merge = (BeltBufferedSegment)CurrentSegmentAt(MergeCell);
            var internalItems = next.Segments[InternalSegmentIndex(next)].CaptureItems();
            if (prunedPlace == "bufferItem")
            {
                Assert.IsFalse(merge.Buffer.HasItem, "pruned buffer item is skipped");
                Assert.AreEqual(1, internalItems.Length);
                AssertRestored(internalItems[0], ItemC, BeltEntryDirection.FromLeft, 100);
                return;
            }
            Assert.IsEmpty(internalItems, "pruned internal item is skipped");
            Assert.IsTrue(merge.Buffer.TryGetItem(out var held));
            Assert.AreEqual(ItemB, held.ItemId);
        }

        [TestCase("itemGuid", "\"not-a-guid\"")]
        [TestCase("itemGuid", "\"11111111-2222-3333-4444-555555555555\"")]
        [TestCase("entryDirection", "12")]
        [TestCase("entryDirection", "-1")]
        [TestCase("distanceToExit", "360")]
        [TestCase("distanceToExit", "-1")]
        public void CorruptedItemDropsWholeSegmentButKeepsOtherSegments(string field, string valueJson)
        {
            var world = NewWorld();
            PlaceStraightLine(world, 0);
            PlaceStraightLine(world, 5);
            Rebuild();
            CurrentSegmentAt(Vector3Int.zero).RestoreItems(new[]
            {
                new BeltItemState(NewItem(ItemA, BeltEntryDirection.FromBack), 50),
                new BeltItemState(NewItem(ItemB, BeltEntryDirection.FromBack), 500)
            });
            CurrentSegmentAt(new Vector3Int(5, 0, 0)).RestoreItems(new[] { new BeltItemState(NewItem(ItemC, BeltEntryDirection.FromBack), 50) });

            // x=0のz=1のアイテム1個だけを壊す
            // Corrupt only the one item on x=0, z=1
            var saved = SaveWorld(world);
            SavedStateAt(saved, new Vector3Int(0, 0, 1))["items"][0][field] = JToken.Parse(valueJson);

            // 壊れたblockを含むx=0のsegmentは、無傷なz=2のアイテムも含めて空。x=0とは別のx=5は残る
            // The x=0 segment holding the corrupted block is empty, intact z=2 item included; the separate x=5 segment keeps its item
            NewWorld().LoadBlockDataList(saved);
            LogAssert.Expect(LogType.Error, new Regex("unreadable saved items"));
            Rebuild();
            Assert.IsEmpty(CurrentSegmentAt(Vector3Int.zero).CaptureItems());
            var other = CurrentSegmentAt(new Vector3Int(5, 0, 0)).CaptureItems();
            Assert.AreEqual(1, other.Length);
            AssertRestored(other[0], ItemC, BeltEntryDirection.FromBack, 50);
        }

        [TestCase("[{\"inputDirection\":4,\"item\":{\"itemGuid\":\"GUID\",\"entryDirection\":2,\"distanceToExit\":100}}]")]
        [TestCase("[{\"inputDirection\":2,\"item\":null}]")]
        [TestCase("[null]")]
        [TestCase("[{\"inputDirection\":2,\"item\":{\"itemGuid\":\"GUID\",\"entryDirection\":2,\"distanceToExit\":100}},{\"inputDirection\":2,\"item\":{\"itemGuid\":\"GUID\",\"entryDirection\":2,\"distanceToExit\":200}}]")]
        public void CorruptedInternalItemDropsMergeItems(string internalItemsJson)
        {
            var world = MachineMergeWorld();
            Rebuild();
            ((BeltBufferedSegment)CurrentSegmentAt(MergeCell)).Buffer.RestoreItem(NewItem(ItemB, BeltEntryDirection.FromBack));
            var saved = SaveWorld(world);
            var guid = MasterHolder.ItemMaster.GetItemGuid(ItemA).ToString();
            SavedStateAt(saved, MergeCell)["internalItems"] = JToken.Parse(internalItemsJson.Replace("GUID", guid));

            // 内部枠が壊れた合流blockは、bufferも内部segmentも復元しない
            // A merge block whose internal list is corrupted restores neither its buffer nor its internal segment
            var loaded = NewWorld();
            InstallMachineMergePorts();
            loaded.LoadBlockDataList(saved);
            LogAssert.Expect(LogType.Error, new Regex("unreadable saved items"));
            Rebuild();
            var assembly = Datastore().Assembly;
            Assert.IsFalse(((BeltBufferedSegment)CurrentSegmentAt(MergeCell)).Buffer.HasItem);
            Assert.IsEmpty(assembly.Segments[InternalSegmentIndex(assembly)].CaptureItems());
        }

        [Test]
        public void SaveRightAfterLoadReturnsLoadedContentUnchanged()
        {
            var world = MergeWorld();
            Rebuild();
            RotateMergeOrderByPassingOneItem(NewItem(ItemA, BeltEntryDirection.FromBack));
            ((BeltBufferedSegment)CurrentSegmentAt(MergeCell)).Buffer.RestoreItem(NewItem(ItemB, BeltEntryDirection.FromRight));
            var saved = SaveWorld(world);

            // ロード完了時の再構築を経た直後のセーブは、ロードした内容と一致する
            // A save right after the load-completion rebuild matches the loaded content
            var loaded = NewWorld();
            loaded.LoadBlockDataList(saved);
            Rebuild();
            foreach (var position in new[] { new Vector3Int(0, 0, -1), Vector3Int.zero, MergeCell, new Vector3Int(0, 0, 2), new Vector3Int(1, 0, 1) })
            {
                var resaved = JToken.FromObject(SaveStateAt(loaded, position));
                Assert.IsTrue(JToken.DeepEquals(SavedStateAt(saved, position), resaved), $"state of {position}: {resaved}");
            }
        }
    }
}
