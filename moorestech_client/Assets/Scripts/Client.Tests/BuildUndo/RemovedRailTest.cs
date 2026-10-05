using Game.Block.Interface;
using Game.Train.RailGraph;
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.Train.RailGraph;
using Game.Train.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    /// <summary>
    ///     種類付きレール区間だけを記録対象とすることを検証
    ///     Verifies rail removal records cover only edges with a drawn tool kind
    /// </summary>
    public class RemovedRailTest
    {
        [Test]
        public void EdgeWithRailTypeIsRecordedAndRestoredByDestination()
        {
            // 種類付きは両端と種類で引き直す
            // A typed edge is restored by both endpoints and its kind
            var railType = Guid.NewGuid();
            var cache = CreateTwoPierCache(railType);
            var sender = new FakeRemovalRestoreSender();

            var result = RemovedRail.Create(cache, 0, 2);
            Assert.AreEqual(RemovedRailCreateOutcome.Created, result.Outcome);
            result.Rail.SendConnectionRestore(sender);

            CollectionAssert.AreEqual(new[] { $"rail:{new Vector3Int(0, 0, 0)}-{new Vector3Int(10, 0, 0)}:{railType}" }, sender.Sent);
        }

        [Test]
        public void EdgeWithEmptyRailTypeIsNotRecorded()
        {
            // 駅の自動レール(Empty)は記録しない
            // Station auto rails (Empty kind) are not recorded
            var cache = CreateTwoPierCache(Guid.Empty);
            Assert.AreEqual(RemovedRailCreateOutcome.FreeSegment, RemovedRail.Create(cache, 0, 2).Outcome);
        }

        [Test]
        public void UnsyncedNodeIsNotRecorded()
        {
            // 未同期のノードを指す区間は記録しない
            // An edge pointing at an unsynced node is not recorded
            Assert.AreEqual(RemovedRailCreateOutcome.NodeNotSynced, RemovedRail.Create(RailGraphClientCache.CreateForEditorTest(), 0, 2).Outcome);
        }

        [Test]
        public void StationInternalEdgeIsNotRecordedEvenWithRailType()
        {
            var cache = CreateTwoPierCache(Guid.NewGuid());
            cache.TryGetNode(0, out var from);
            cache.TryGetNode(2, out var to);

            // 同一駅の内部区間は復元対象外
            // Internal edges of one station are not restored
            from.StationRef.SetStationReference(new BlockInstanceId(7), Vector3Int.zero, StationNodeRole.Entry, StationNodeSide.Front);
            to.StationRef.SetStationReference(new BlockInstanceId(7), Vector3Int.zero, StationNodeRole.Exit, StationNodeSide.Front);
            Assert.AreEqual(RemovedRailCreateOutcome.StationInternal, RemovedRail.Create(cache, 0, 2).Outcome);
        }

        private static RailGraphClientCache CreateTwoPierCache(Guid railType)
        {
            var cache = RailGraphClientCache.CreateForEditorTest();
            UpsertPier(cache, 0, new Vector3Int(0, 0, 0));
            UpsertPier(cache, 2, new Vector3Int(10, 0, 0));
            cache.UpsertConnection(0, 2, 10, railType, true);
            cache.UpsertConnection(3, 1, 10, railType, true);
            return cache;
        }

        private static void UpsertPier(RailGraphClientCache cache, int frontNodeId, Vector3Int blockPosition)
        {
            var origin = (Vector3)blockPosition;
            cache.UpsertNode(frontNodeId, Guid.NewGuid(), origin, new ConnectionDestination(blockPosition, 0, true), origin + Vector3.forward, origin + Vector3.back);
            cache.UpsertNode(frontNodeId + 1, Guid.NewGuid(), origin, new ConnectionDestination(blockPosition, 0, false), origin + Vector3.back, origin + Vector3.forward);
        }
    }
}
