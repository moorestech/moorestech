using System;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.BeltConnection.Sync
{
    // 全量のハッシュが等しい入力で安定し、どの1項目を変えても変わるか
    // Whether the full-state hash is stable for equal inputs and changes whenever any single field changes
    public class BeltTransportStateHashTest
    {
        [Test]
        public void EqualInputsGiveEqualHash()
        {
            Assert.AreEqual(Hash(new Spec()), Hash(new Spec()));
        }

        [Test]
        public void EmptyStateHashIsDeterministicAndDiffersFromNonEmpty()
        {
            var empty = BeltTransportStateHash.Compute(new BeltTransportFullState(Array.Empty<BeltSegmentState>()));
            Assert.AreEqual(empty, BeltTransportStateHash.Compute(new BeltTransportFullState(Array.Empty<BeltSegmentState>())));
            Assert.AreNotEqual(empty, Hash(new Spec()));
        }

        // 1項目だけ変えた全量は元とハッシュが異なる
        // A full state with exactly one field changed hashes differently from the original
        [TestCase("ItemDistance")]
        [TestCase("ItemId")]
        [TestCase("ItemInstance")]
        [TestCase("ItemEntry")]
        [TestCase("NoRunningItem")]
        [TestCase("PriorityOrder")]
        [TestCase("CellPosition")]
        [TestCase("CellForward")]
        [TestCase("Speed")]
        [TestCase("Forward")]
        [TestCase("Kind")]
        [TestCase("Internal")]
        [TestCase("OutputPartner")]
        [TestCase("OutputDirection")]
        [TestCase("OutputEntry")]
        [TestCase("InputPartner")]
        [TestCase("NoBuffer")]
        [TestCase("BufferInstance")]
        [TestCase("BufferEntry")]
        public void ChangingOneFieldChangesHash(string field)
        {
            var changed = new Spec();
            switch (field)
            {
                case "ItemDistance": changed.ItemDistance = 101; break;
                case "ItemId": changed.ItemId = new ItemId(2); break;
                case "ItemInstance": changed.ItemInstance = new ItemInstanceId(7); break;
                case "ItemEntry": changed.ItemEntry = BeltEntryDirection.FromLeft; break;
                case "NoRunningItem": changed.HasRunningItem = false; break;
                case "PriorityOrder": changed.MergeOrder = BeltPriority.MoveLast(changed.MergeOrder, (int)BeltDirection.Back); break;
                case "CellPosition": changed.CellPosition = new Vector3Int(0, 0, 1); break;
                case "CellForward": changed.CellForward = BeltDirection.Left; break;
                case "Speed": changed.Speed = 9; break;
                case "Forward": changed.Forward = BeltDirection.Right; break;
                case "Kind": changed.MergeKind = BeltSegmentKind.Branch; break;
                case "Internal": changed.FirstIsInternal = true; break;
                case "OutputPartner": changed.OutputPartner = BeltLinkShape.Machine; break;
                case "OutputDirection": changed.OutputDirection = BeltDirection.Left; break;
                case "OutputEntry": changed.OutputEntry = BeltEntryDirection.FromBackBelow; break;
                case "InputPartner": changed.InputPartner = BeltLinkShape.Machine; break;
                case "NoBuffer": changed.HasBuffer = false; break;
                case "BufferInstance": changed.BufferInstance = new ItemInstanceId(8); break;
                case "BufferEntry": changed.BufferEntry = BeltEntryDirection.FromLeft; break;
                default: Assert.Fail($"unknown field {field}"); break;
            }
            Assert.AreNotEqual(Hash(new Spec()), Hash(changed), field);
        }

        [Test]
        public void SwappingSegmentOrderChangesHash()
        {
            var spec = new Spec();
            var state = spec.Build();
            var swapped = new BeltTransportFullState(new[] { state.Segments[1], state.Segments[0] });
            Assert.AreNotEqual(BeltTransportStateHash.Compute(state), BeltTransportStateHash.Compute(swapped));
        }

        private static uint Hash(Spec spec)
        {
            return BeltTransportStateHash.Compute(spec.Build());
        }

        // 2本(1マスの通常segment → 合流segment)の全量を項目から組む。既定値を1つだけ書き換えて差を作る
        // Builds a two-segment full state (one-cell normal -> merge) from fields; a difference is made by overwriting one default
        private sealed class Spec
        {
            public int ItemDistance = 100;
            public ItemId ItemId = new(1);
            public ItemInstanceId ItemInstance = new(5);
            public BeltEntryDirection ItemEntry = BeltEntryDirection.FromBack;
            public bool HasRunningItem = true;
            public int MergeOrder = BeltPriority.Create(BeltDirection.Back);
            public Vector3Int CellPosition = Vector3Int.zero;
            public BeltDirection CellForward = BeltDirection.Front;
            public int Speed = 8;
            public BeltDirection Forward = BeltDirection.Front;
            public BeltSegmentKind MergeKind = BeltSegmentKind.Merge;
            public bool FirstIsInternal;
            public int OutputPartner = 1;
            public BeltDirection OutputDirection = BeltDirection.Front;
            public BeltEntryDirection OutputEntry = BeltEntryDirection.FromBack;
            public int InputPartner;
            public bool HasBuffer = true;
            public ItemInstanceId BufferInstance = new(6);
            public BeltEntryDirection BufferEntry = BeltEntryDirection.FromBack;

            public BeltTransportFullState Build()
            {
                // 通常segment: 1マス、合流へ出力1本、走行中アイテム1個
                // Normal segment: one cell, one output into the merge, one running item
                var normalShape = new BeltSegmentShape(BeltSegmentKind.Normal, FirstIsInternal, new[] { new BeltCellShape(CellPosition, CellForward) }, Speed, Forward,
                    Array.Empty<BeltLinkShape>(), new[] { new BeltLinkShape(OutputDirection, OutputEntry, OutputPartner) });
                var running = HasRunningItem
                    ? new[] { new BeltItemSnapshot(new BeltItem(ItemId, ItemInstance, ItemEntry), ItemDistance) }
                    : Array.Empty<BeltItemSnapshot>();
                var normal = new BeltSegmentState(normalShape, 0, running, false, default);

                // 合流segment: 通常segmentからの入力1本、buffer内アイテム1個
                // Merge segment: one input from the normal segment, one buffered item
                var mergeShape = new BeltSegmentShape(MergeKind, false, new[] { new BeltCellShape(new Vector3Int(0, 0, 2), BeltDirection.Front) }, 8, BeltDirection.Front,
                    new[] { new BeltLinkShape(BeltDirection.Back, BeltEntryDirection.FromBack, InputPartner) }, Array.Empty<BeltLinkShape>());
                var buffered = new BeltItemSnapshot(new BeltItem(new ItemId(3), BufferInstance, BufferEntry), 0);
                var merge = new BeltSegmentState(mergeShape, MergeOrder, Array.Empty<BeltItemSnapshot>(), HasBuffer, HasBuffer ? buffered : default);
                return new BeltTransportFullState(new[] { normal, merge });
            }
        }
    }
}
