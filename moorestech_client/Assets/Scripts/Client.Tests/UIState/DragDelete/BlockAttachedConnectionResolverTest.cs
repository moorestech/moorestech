using System;
using System.Text.RegularExpressions;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Game.Block.Interface;
using Game.Train.SaveLoad;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UIState
{
    public class BlockAttachedConnectionResolverTest
    {
        private GameObject _blockObject;

        [TearDown]
        public void TearDown()
        {
            if (_blockObject != null) UnityEngine.Object.DestroyImmediate(_blockObject);
        }

        [Test]
        public void RequesterDestroyedWhileRequestingIsPurgedOnTopologyChange()
        {
            var cache = RailGraphClientCache.CreateForEditorTest();
            UpsertPier(cache, 0, Vector3Int.zero);
            UpsertPier(cache, 2, new Vector3Int(10, 0, 0));
            var resolver = new BlockAttachedConnectionResolver(new ConnectionLineRegistry(), cache);
            var block = CreatePierBlock(Vector3Int.zero);
            resolver.RequestCascadePreview(block);

            // 赤要求中にブロックが破棄されても、以後のレール変化で例外を投げない
            // A block destroyed while requesting red must not throw on later rail changes
            UnityEngine.Object.DestroyImmediate(_blockObject);
            LogAssert.Expect(LogType.Log, new Regex("\\[RemovalPreview\\] requester destroyed while requesting"));
            Assert.DoesNotThrow(() => cache.UpsertConnection(0, 2, 10, Guid.NewGuid(), true));
            Assert.DoesNotThrow(() => cache.UpsertConnection(3, 1, 10, Guid.NewGuid(), true));
        }

        [Test]
        public void UnsyncedDestinationIsCountedAsUnrecordable()
        {
            var resolver = new BlockAttachedConnectionResolver(new ConnectionLineRegistry(), RailGraphClientCache.CreateForEditorTest());
            var block = CreatePierBlock(Vector3Int.zero);
            var collector = new RemovedObjectCollector();

            // 同じ未同期端点でも赤表示は情報、Undo採取は記録不能の警告に分ける
            // Report the same unsynced endpoint as info in preview and as an unrecordable warning in undo capture
            LogAssert.Expect(LogType.Log, new Regex("\\[RemovalPreview\\] rail node not synced yet:.*retry on next topology change"));
            resolver.RequestCascadePreview(block);
            LogAssert.Expect(LogType.Warning, new Regex("\\[RemovalRestore\\] unrecordable: rail at .*: node not synced"));
            resolver.CollectRemovedConnections(block, collector);
            Assert.IsEmpty(collector.Objects);
            Assert.AreEqual(1, collector.UnrecordableCount);
        }

        [Test]
        public void NodeSyncAloneRetriesUnsyncedDestinations()
        {
            var cache = RailGraphClientCache.CreateForEditorTest();
            var resolver = new BlockAttachedConnectionResolver(new ConnectionLineRegistry(), cache);
            var block = CreatePierBlock(Vector3Int.zero);
            AddArea(block, false);
            var unsynced = new Regex("\\[RemovalPreview\\] rail node not synced yet:.*retry on next topology change");
            LogAssert.Expect(LogType.Log, unsynced);
            LogAssert.Expect(LogType.Log, unsynced);
            resolver.RequestCascadePreview(block);

            // 接続差分が来なくても、ノード同期だけで未同期端点を取り直す（残る背面1件だけ再ログ）
            // Retry unsynced destinations on a node sync alone, without an edge diff (only the remaining back side logs again)
            LogAssert.Expect(LogType.Log, new Regex("^UpsertNode: nodeId=0"));
            LogAssert.Expect(LogType.Log, unsynced);
            cache.UpsertNode(0, Guid.NewGuid(), Vector3.zero, new ConnectionDestination(Vector3Int.zero, 0, true), Vector3.forward, Vector3.back);
            LogAssert.NoUnexpectedReceived();
        }

        private BlockGameObject CreatePierBlock(Vector3Int origin)
        {
            _blockObject = new GameObject("Pier");
            var block = _blockObject.AddComponent<BlockGameObject>();
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).GetSetMethod(true)
                .Invoke(block, new object[] { new BlockPositionInfo(origin, BlockDirection.North, Vector3Int.one) });
            AddArea(block, true);
            return block;
        }

        private void AddArea(BlockGameObject block, bool isFront)
        {
            var area = new GameObject(isFront ? "Front" : "Back", typeof(BoxCollider)).AddComponent<TrainRailConnectAreaCollider>();
            area.transform.SetParent(_blockObject.transform);
            area.isFront = isFront;
            area.Initialize(block);
        }

        private static void UpsertPier(RailGraphClientCache cache, int frontNodeId, Vector3Int position)
        {
            var origin = (Vector3)position;
            cache.UpsertNode(frontNodeId, Guid.NewGuid(), origin, new ConnectionDestination(position, 0, true), origin + Vector3.forward, origin + Vector3.back);
            cache.UpsertNode(frontNodeId + 1, Guid.NewGuid(), origin, new ConnectionDestination(position, 0, false), origin + Vector3.back, origin + Vector3.forward);
        }
    }
}
