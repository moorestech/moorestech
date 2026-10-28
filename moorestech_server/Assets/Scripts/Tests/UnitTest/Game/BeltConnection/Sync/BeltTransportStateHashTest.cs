using System;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.BeltConnection.Sync
{
    // 組のハッシュが等しい入力で安定し、どの1項目を変えても変わるか。全量から Core segment を組み、本番と同じ直接の経路で混ぜる
    // Whether the assembly hash is stable for equal inputs and changes whenever any single field changes; Core segments are assembled from the full state and mixed through the same direct path as production
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
            var empty = Hash(new BeltTransportFullState(Array.Empty<BeltSegmentState>()));
            Assert.AreEqual(empty, Hash(new BeltTransportFullState(Array.Empty<BeltSegmentState>())));
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
        [TestCase("Capacity")]
        [TestCase("Speed")]
        [TestCase("CurrentSpeed")]
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
                case "Capacity": changed.Capacity = 2; break;
                case "Speed": changed.Speed = 9; break;
                case "CurrentSpeed": changed.CurrentSpeed = 9; break;
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

        // 同じ segment でも番号が入れ替われば別の値になる(配線はそのまま、並びだけ入れ替える)
        // The same segments hash differently once their indices swap (wiring untouched, only the order changes)
        [Test]
        public void SwappingSegmentOrderChangesHash()
        {
            var replica = BeltTransportReplicaAssembler.Assemble(new Spec().Build());
            var swappedShapes = new[] { replica.Shapes[1], replica.Shapes[0] };
            var swappedSegments = new[] { replica.Segments[1], replica.Segments[0] };
            Assert.AreNotEqual(BeltTransportStateHash.Compute(replica.Shapes, replica.Segments), BeltTransportStateHash.Compute(swappedShapes, swappedSegments));
        }

        private static uint Hash(Spec spec)
        {
            return Hash(spec.Build());
        }

        private static uint Hash(BeltTransportFullState fullState)
        {
            var replica = BeltTransportReplicaAssembler.Assemble(fullState);
            return BeltTransportStateHash.Compute(replica.Shapes, replica.Segments);
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
            public int Capacity = 1;
            public int Speed = 8;
            public int CurrentSpeed = 8;
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
                var normalShape = new BeltSegmentShape(BeltSegmentKind.Normal, FirstIsInternal, new[] { new BeltCellShape(CellPosition, CellForward) }, Capacity, Speed, Forward,
                    Array.Empty<BeltLinkShape>(), new[] { new BeltLinkShape(OutputDirection, OutputEntry, OutputPartner) });
                var running = HasRunningItem
                    ? new[] { new BeltItemSnapshot(new BeltItem(ItemId, ItemInstance, ItemEntry), ItemDistance) }
                    : Array.Empty<BeltItemSnapshot>();
                var normal = new BeltSegmentState(normalShape, 0, CurrentSpeed, running, false, default);

                // 合流segment: 通常segmentからの入力1本、buffer内アイテム1個
                // Merge segment: one input from the normal segment, one buffered item
                var mergeShape = new BeltSegmentShape(MergeKind, false, new[] { new BeltCellShape(new Vector3Int(0, 0, 2), BeltDirection.Front) }, 1, 8, BeltDirection.Front,
                    new[] { new BeltLinkShape(BeltDirection.Back, BeltEntryDirection.FromBack, InputPartner) }, Array.Empty<BeltLinkShape>());
                var buffered = new BeltItemSnapshot(new BeltItem(new ItemId(3), BufferInstance, BufferEntry), 0);
                var merge = new BeltSegmentState(mergeShape, MergeOrder, 8, Array.Empty<BeltItemSnapshot>(), HasBuffer, HasBuffer ? buffered : default);
                return new BeltTransportFullState(new[] { normal, merge });
            }
        }
    }
}
