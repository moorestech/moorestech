using System.Collections.Generic;
using Game.Block.Interface;
using Game.Train.RailGraph;
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.Train.RailGraph;
using Game.Train.SaveLoad;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BuildUndo
{
    /// <summary>
    ///     種類付きレール区間だけを記録対象とすることを検証
    ///     Verifies rail removal records cover only edges with a drawn tool kind
    /// </summary>
    public class RemovedRailTest
    {
        private GameObject _railObject;

        [TearDown]
        public void TearDown()
        {
            if (_railObject != null) UnityEngine.Object.DestroyImmediate(_railObject);
        }

        [Test]
        public void EdgeWithRailTypeIsRecordedAndRestoredByDestination()
        {
            // 種類付きは両端と種類で引き直す
            // A typed edge is restored by both endpoints and its kind
            var railType = Guid.NewGuid();
            var cache = CreateTwoPierCache(railType);
            var sender = new FakeRemovalRestoreSender();

            var collector = new RemovedObjectCollector();
            RemovedRail.Capture(cache, 0, 2, RemovedRailCaptureContext.Direct, collector);
            Assert.AreEqual(1, collector.Objects.Count);
            collector.Objects[0].TrySendConnectionRestore(sender, new HashSet<Vector3Int>());

            CollectionAssert.AreEqual(new[] { $"rail:{new Vector3Int(0, 0, 0)}-{new Vector3Int(10, 0, 0)}:{railType}" }, sender.Sent);
        }

        [Test]
        public void DirectEmptyRailIsCountedButCascadeEmptyRailIsIgnored()
        {
            // 無償レールは直接切断時だけUndo失敗件数へ入れる
            // A free edge counts as skipped only when directly cut
            var cache = CreateTwoPierCache(Guid.Empty);
            var direct = new RemovedObjectCollector();
            LogAssert.Expect(LogType.Warning, "[RemovalRestore] unrecordable: costless rail 0->2: direct restore unavailable");
            RemovedRail.Capture(cache, 0, 2, RemovedRailCaptureContext.Direct, direct);
            Assert.AreEqual(1, direct.UnrecordableCount);
            var cascade = new RemovedObjectCollector();
            RemovedRail.Capture(cache, 0, 2, RemovedRailCaptureContext.Cascade, cascade);
            Assert.AreEqual(0, cascade.UnrecordableCount);
        }

        [Test]
        public void UnsyncedNodeIsNotRecorded()
        {
            // 未同期のノードを指す区間は記録しない
            // An edge pointing at an unsynced node is not recorded
            var collector = new RemovedObjectCollector();
            LogAssert.Expect(LogType.Warning, "[RemovalRestore] unrecordable: rail 0->2: node not synced");
            RemovedRail.Capture(RailGraphClientCache.CreateForEditorTest(), 0, 2, RemovedRailCaptureContext.Cascade, collector);
            Assert.AreEqual(1, collector.UnrecordableCount);
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
            var collector = new RemovedObjectCollector();
            RemovedRail.Capture(cache, 0, 2, RemovedRailCaptureContext.Direct, collector);
            Assert.IsEmpty(collector.Objects);
            Assert.AreEqual(0, collector.UnrecordableCount);
        }

        [Test]
        public void DeleteTargetRailCapturesDirectEdgeThroughItsCarrier()
        {
            var railType = Guid.NewGuid();
            var cache = CreateTwoPierCache(railType);
            _railObject = new GameObject("DirectRailTarget");
            var carrier = _railObject.AddComponent<RailObjectIdCarrier>();
            carrier.SetRailObjectId(RailObjectIdCodec.ComputeRailObjectId(0, 2));
            var target = _railObject.AddComponent<DeleteTargetRail>();
            target.SetRailGraphCache(cache);

            var collector = new RemovedObjectCollector();
            target.CollectRemovedObjects(collector);
            Assert.AreEqual(1, collector.Objects.Count);
            Assert.AreEqual(0, collector.UnrecordableCount);
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
