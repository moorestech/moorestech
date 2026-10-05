using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Game.Block.Interface;
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
        private GameObject _pierObject;

        [TearDown]
        public void TearDown()
        {
            if (_pierObject != null) UnityEngine.Object.DestroyImmediate(_pierObject);
        }

        [Test]
        public void OnePhysicalRailBetweenTwoPiersIsEnumeratedOnce()
        {
            // 橋脚AとBを往復1組で結ぶ
            // Connect piers A and B with one round-trip pair
            var cache = RailGraphClientCache.CreateForEditorTest();
            var pierA = new Vector3Int(0, 0, 0);
            var pierB = new Vector3Int(10, 0, 0);
            UpsertPier(cache, 0, pierA, 0);
            UpsertPier(cache, 2, pierB, 0);
            var railType = Guid.NewGuid();
            cache.UpsertConnection(0, 2, 10, railType, true);
            cache.UpsertConnection(3, 1, 10, railType, true);

            var edgesOfA = new List<(int, int)>();
            var unsynced = new List<ConnectionDestination>();
            AttachedRailEdgeEnumerator.Collect(cache, Destinations(pierA, 0), edgesOfA, unsynced);
            var edgesOfB = new List<(int, int)>();
            AttachedRailEdgeEnumerator.Collect(cache, Destinations(pierB, 0), edgesOfB, unsynced);

            // 両側から引いても同じcanonical区間1件になる
            // Either side yields the same single canonical edge
            Assert.AreEqual(1, edgesOfA.Count);
            CollectionAssert.AreEqual(new[] { (0, 2) }, edgesOfA);
            CollectionAssert.AreEqual(edgesOfA, edgesOfB);
            Assert.IsEmpty(unsynced);
        }

        [Test]
        public void BlockWithoutRailsYieldsNothing()
        {
            // レールを持たないブロックは何も列挙しない
            // A block with no rails enumerates nothing
            var edges = new List<(int, int)>();
            var unsynced = new List<ConnectionDestination>();
            AttachedRailEdgeEnumerator.Collect(RailGraphClientCache.CreateForEditorTest(), Array.Empty<ConnectionDestination>(), edges, unsynced);
            Assert.AreEqual(0, edges.Count);
            Assert.IsEmpty(unsynced);
        }

        [Test]
        public void MissingNodeIsReportedAsUnsyncedDestination()
        {
            var cache = RailGraphClientCache.CreateForEditorTest();
            UpsertPier(cache, 0, Vector3Int.zero, 0);
            var missing = new ConnectionDestination(Vector3Int.right, 0, true);
            var destinations = new[] { new ConnectionDestination(Vector3Int.zero, 0, true), missing };
            var edges = new List<(int, int)>();
            var unsynced = new List<ConnectionDestination>();

            // 同期済み端点は残し、欠けた端点だけを呼び出し元へ返す
            // Keep synced destinations and return only the missing one to the caller
            AttachedRailEdgeEnumerator.Collect(cache, destinations, edges, unsynced);
            Assert.IsEmpty(edges);
            CollectionAssert.AreEqual(new[] { missing }, unsynced);
        }

        [Test]
        public void TwoDirectionsWithinSameBlockAreDeduplicated()
        {
            // 同一ブロック内の往復辺も1本扱い
            // Paired edges within one block count as one rail
            var cache = RailGraphClientCache.CreateForEditorTest();
            UpsertPier(cache, 0, Vector3Int.zero, 0);
            UpsertPier(cache, 2, Vector3Int.zero, 1);
            var railType = Guid.NewGuid();
            cache.UpsertConnection(0, 2, 10, railType, true);
            cache.UpsertConnection(3, 1, 10, railType, true);

            var edges = new List<(int, int)>();
            var destinations = new List<ConnectionDestination>(Destinations(Vector3Int.zero, 0));
            destinations.AddRange(Destinations(Vector3Int.zero, 1));
            var unsynced = new List<ConnectionDestination>();
            AttachedRailEdgeEnumerator.Collect(cache, destinations, edges, unsynced);
            CollectionAssert.AreEqual(new[] { (0, 2) }, edges);
            Assert.IsEmpty(unsynced);
        }

        [Test]
        public void SparseNodesAndUnrelatedBlocksDoNotHideAttachedBranches()
        {
            // ID欠番と無関係な辺を含む分岐を作る
            // Build branches with id gaps and an unrelated edge
            var cache = RailGraphClientCache.CreateForEditorTest();
            UpsertPier(cache, 4, Vector3Int.zero, 0);
            UpsertPier(cache, 8, Vector3Int.right, 0);
            UpsertPier(cache, 10, Vector3Int.left, 0);
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
            var unsynced = new List<ConnectionDestination>();
            AttachedRailEdgeEnumerator.Collect(cache, Destinations(Vector3Int.zero, 0), edges, unsynced);
            CollectionAssert.AreEquivalent(new[] { (4, 8), (4, 10) }, edges);
            Assert.IsEmpty(unsynced);
        }

        [Test]
        public void PierCollidersPublishBothRailDestinations()
        {
            _pierObject = new GameObject("PierSource");
            var block = _pierObject.AddComponent<BlockGameObject>();
            var position = new Vector3Int(4, 0, 7);
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).GetSetMethod(true)
                .Invoke(block, new object[] { new BlockPositionInfo(position, BlockDirection.North, Vector3Int.one) });
            var front = new GameObject("Front", typeof(BoxCollider)).AddComponent<TrainRailConnectAreaCollider>();
            front.transform.SetParent(_pierObject.transform);
            front.isFront = true;
            front.Initialize(block);
            var back = new GameObject("Back", typeof(BoxCollider)).AddComponent<TrainRailConnectAreaCollider>();
            back.transform.SetParent(_pierObject.transform);
            back.isFront = false;
            back.Initialize(block);

            // 橋脚の既存コライダーから端点を得る
            // Read pier endpoints from the existing colliders
            var destinations = new List<ConnectionDestination>();
            foreach (var area in block.GetComponentsInChildren<IRailComponentConnectAreaCollider>())
                destinations.Add(area.CreateConnectionDestination());
            CollectionAssert.AreEquivalent(Destinations(position, 0), destinations);
        }

        [Test]
        public void StationColliderPublishesItsOwnRailDestination()
        {
            _pierObject = new GameObject("StationSource");
            var block = _pierObject.AddComponent<BlockGameObject>();
            var position = new Vector3Int(8, 0, 4);
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).GetSetMethod(true)
                .Invoke(block, new object[] { new BlockPositionInfo(position, BlockDirection.North, Vector3Int.one) });
            var source = _pierObject.AddComponent<StationRailConnectAreaCollider>();
            typeof(StationRailConnectAreaCollider).GetField("railComponentIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(source, StationrailComponentIndex.Index1);
            source.Initialize(block);

            var destinations = new List<ConnectionDestination>();
            foreach (var area in block.GetComponentsInChildren<IRailComponentConnectAreaCollider>())
                destinations.Add(area.CreateConnectionDestination());
            CollectionAssert.AreEqual(new[] { new ConnectionDestination(position, 1, true) }, destinations);
        }

        private static ConnectionDestination[] Destinations(Vector3Int position, int componentIndex)
        {
            return new[] { new ConnectionDestination(position, componentIndex, true), new ConnectionDestination(position, componentIndex, false) };
        }

        private static void UpsertPier(RailGraphClientCache cache, int frontNodeId, Vector3Int blockPosition, int componentIndex)
        {
            var origin = (Vector3)blockPosition;
            cache.UpsertNode(frontNodeId, Guid.NewGuid(), origin, new ConnectionDestination(blockPosition, componentIndex, true), origin + Vector3.forward, origin + Vector3.back);
            cache.UpsertNode(frontNodeId + 1, Guid.NewGuid(), origin, new ConnectionDestination(blockPosition, componentIndex, false), origin + Vector3.back, origin + Vector3.forward);
        }
    }
}
