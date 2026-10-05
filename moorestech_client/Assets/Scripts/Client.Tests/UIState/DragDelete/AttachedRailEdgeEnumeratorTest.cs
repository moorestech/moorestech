using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Game.Train.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     ブロック座標のレール区間の列挙を検証
    ///     Verifies rail edges at a block position enumerate once per physical rail
    /// </summary>
    public class AttachedRailEdgeEnumeratorTest
    {
        [Test]
        public void OnePhysicalRailBetweenTwoPiersIsEnumeratedOnce()
        {
            // 橋脚AとBを往復1組で結ぶ
            // Connect piers A and B with one round-trip pair
            var cache = RailGraphClientCache.CreateForEditorTest();
            var pierA = new Vector3Int(0, 0, 0);
            var pierB = new Vector3Int(10, 0, 0);
            UpsertPier(cache, 0, pierA);
            UpsertPier(cache, 2, pierB);
            var railType = Guid.NewGuid();
            cache.UpsertConnection(0, 2, 10, railType, true);
            cache.UpsertConnection(3, 1, 10, railType, true);

            var edgesOfA = new List<(int, int)>();
            AttachedRailEdgeEnumerator.Collect(cache, pierA, edgesOfA);
            var edgesOfB = new List<(int, int)>();
            AttachedRailEdgeEnumerator.Collect(cache, pierB, edgesOfB);

            // 両側から引いても同じcanonical区間1件になる
            // Either side yields the same single canonical edge
            Assert.AreEqual(1, edgesOfA.Count);
            CollectionAssert.AreEqual(new[] { (0, 2) }, edgesOfA);
            CollectionAssert.AreEqual(edgesOfA, edgesOfB);
        }

        [Test]
        public void BlockWithoutRailsYieldsNothing()
        {
            // レールを持たないブロックは何も列挙しない
            // A block with no rails enumerates nothing
            var edges = new List<(int, int)>();
            AttachedRailEdgeEnumerator.Collect(RailGraphClientCache.CreateForEditorTest(), Vector3Int.one, edges);
            Assert.AreEqual(0, edges.Count);
        }

        [Test]
        public void TwoDirectionsWithinSameBlockAreDeduplicated()
        {
            // 同一ブロック内の往復辺も1本扱い
            // Paired edges within one block count as one rail
            var cache = RailGraphClientCache.CreateForEditorTest();
            UpsertPier(cache, 0, Vector3Int.zero);
            UpsertPier(cache, 2, Vector3Int.zero);
            var railType = Guid.NewGuid();
            cache.UpsertConnection(0, 2, 10, railType, true);
            cache.UpsertConnection(3, 1, 10, railType, true);

            var edges = new List<(int, int)>();
            AttachedRailEdgeEnumerator.Collect(cache, Vector3Int.zero, edges);
            CollectionAssert.AreEqual(new[] { (0, 2) }, edges);
        }

        [Test]
        public void SparseNodesAndUnrelatedBlocksDoNotHideAttachedBranches()
        {
            // ID欠番と無関係な辺を含む分岐を作る
            // Build branches with id gaps and an unrelated edge
            var cache = RailGraphClientCache.CreateForEditorTest();
            UpsertPier(cache, 4, Vector3Int.zero);
            UpsertPier(cache, 8, Vector3Int.right);
            UpsertPier(cache, 10, Vector3Int.left);
            var railType = Guid.NewGuid();
            cache.UpsertConnection(4, 8, 10, railType, true);
            cache.UpsertConnection(9, 5, 10, railType, true);
            cache.UpsertConnection(4, 10, 10, railType, true);
            cache.UpsertConnection(11, 5, 10, railType, true);
            cache.UpsertConnection(8, 10, 10, railType, true);
            cache.UpsertConnection(11, 9, 10, railType, true);

            // 列挙は指定ブロックの2本だけを返す
            // Enumeration returns only the two rails attached to the requested block
            var edges = new List<(int, int)>();
            AttachedRailEdgeEnumerator.Collect(cache, Vector3Int.zero, edges);
            CollectionAssert.AreEquivalent(new[] { (4, 8), (4, 10) }, edges);
        }

        private static void UpsertPier(RailGraphClientCache cache, int frontNodeId, Vector3Int blockPosition)
        {
            var origin = (Vector3)blockPosition;
            cache.UpsertNode(frontNodeId, Guid.NewGuid(), origin, new ConnectionDestination(blockPosition, 0, true), origin + Vector3.forward, origin + Vector3.back);
            cache.UpsertNode(frontNodeId + 1, Guid.NewGuid(), origin, new ConnectionDestination(blockPosition, 0, false), origin + Vector3.back, origin + Vector3.forward);
        }
    }
}
