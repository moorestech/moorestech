using System.Collections.Generic;
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Core.Master;
using Game.Block.Interface;
using Game.Train.SaveLoad;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BuildUndo
{
    public class RemovalCaptureLayerTest
    {
        private GameObject _blockObject;
        private GameObject _lineObject;
        private BlockAttachedConnectionResolver _previousResolver;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _previousResolver = ClientDIContext.BlockAttachedConnectionResolver;
        }

        [TearDown]
        public void TearDown()
        {
            SetResolver(_previousResolver);
            if (_lineObject != null) UnityEngine.Object.DestroyImmediate(_lineObject);
            if (_blockObject != null) UnityEngine.Object.DestroyImmediate(_blockObject);
        }

        [Test]
        public void BlockChildCollectsItsBlockAndAttachedLineThroughResolver()
        {
            var registry = new ConnectionLineRegistry();
            SetResolver(new BlockAttachedConnectionResolver(registry, RailGraphClientCache.CreateForEditorTest()));
            _blockObject = new GameObject("RemovedPole");
            var block = _blockObject.AddComponent<BlockGameObject>();
            var blockId = ForUnitTestModBlockId.MachineId;
            SetBlockProperty(block, nameof(BlockGameObject.BlockId), blockId);
            SetBlockProperty(block, nameof(BlockGameObject.BlockInstanceId), new BlockInstanceId(1));
            SetBlockProperty(block, nameof(BlockGameObject.BlockPosInfo),
                new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize));
            var child = _blockObject.AddComponent<BlockGameObjectChild>();
            child.Init(block);

            // 表示線から端点を読み取り、ブロックの巻き込み採取へ流す
            // Resolve displayed line endpoints and pass them through block cascade capture
            _lineObject = new GameObject("AttachedWire");
            var line = _lineObject.AddComponent<ConnectionLineDeleteTarget>();
            var tool = Guid.NewGuid();
            line.Initialize(new BlockInstanceId(1), new BlockInstanceId(2), tool,
                registry, new Endpoints(), new FakeConnectionLineCommands(ConnectionLineKind.ElectricWire));
            var collector = new RemovedObjectCollector();
            child.CollectRemovedObjects(collector);

            Assert.AreEqual(2, collector.Objects.Count);
            var sender = new FakeRemovalRestoreSender();
            collector.Objects[1].TrySendConnectionRestore(sender, new HashSet<Vector3Int>());
            CollectionAssert.AreEqual(new[] { $"wire:{Vector3Int.zero}-{Vector3Int.right}:{tool}" }, sender.Sent);
        }

        [Test]
        public void MissingEndpointIsCountedAsUnrecordable()
        {
            _lineObject = new GameObject("MissingEndpointWire");
            var line = _lineObject.AddComponent<ConnectionLineDeleteTarget>();
            line.Initialize(new BlockInstanceId(1), new BlockInstanceId(2), Guid.NewGuid(),
                new ConnectionLineRegistry(), new MissingEndpoints(), new FakeConnectionLineCommands(ConnectionLineKind.ElectricWire));
            var collector = new RemovedObjectCollector();

            LogAssert.Expect(LogType.Warning, new Regex("\\[RemovalRestore\\] unrecordable: line endpoint block not found"));
            line.CollectRemovedObjects(collector);
            Assert.IsEmpty(collector.Objects);
            Assert.AreEqual(1, collector.UnrecordableCount);
        }

        [Test]
        public void BlockChildCollectsAttachedRailThroughPierSource()
        {
            var cache = RailGraphClientCache.CreateForEditorTest();
            var origin = Vector3Int.zero;
            var other = new Vector3Int(10, 0, 0);
            UpsertPier(cache, 0, origin);
            UpsertPier(cache, 2, other);
            var railType = Guid.NewGuid();
            cache.UpsertConnection(0, 2, 10, railType, true);
            cache.UpsertConnection(3, 1, 10, railType, true);
            SetResolver(new BlockAttachedConnectionResolver(new ConnectionLineRegistry(), cache));
            _blockObject = new GameObject("RemovedRailPier");
            var block = _blockObject.AddComponent<BlockGameObject>();
            var blockId = ForUnitTestModBlockId.MachineId;
            SetBlockProperty(block, nameof(BlockGameObject.BlockId), blockId);
            SetBlockProperty(block, nameof(BlockGameObject.BlockInstanceId), new BlockInstanceId(1));
            SetBlockProperty(block, nameof(BlockGameObject.BlockPosInfo),
                new BlockPositionInfo(origin, BlockDirection.North, MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize));
            var processor = _blockObject.AddComponent<TrainRailStateChangeProcessor>();
            processor.Initialize(block);
            var front = new GameObject("PierFront", typeof(BoxCollider)).AddComponent<TrainRailConnectAreaCollider>();
            front.transform.SetParent(_blockObject.transform);
            front.isFront = true;
            front.Initialize(block);
            var back = new GameObject("PierBack", typeof(BoxCollider)).AddComponent<TrainRailConnectAreaCollider>();
            back.transform.SetParent(_blockObject.transform);
            back.isFront = false;
            back.Initialize(block);
            // 初期化対象外の非アクティブ子を撤去解決へ渡さない
            // Exclude inactive children that block initialization never reached
            var inactive = new GameObject("InactivePierArea", typeof(BoxCollider));
            inactive.transform.SetParent(_blockObject.transform);
            inactive.AddComponent<TrainRailConnectAreaCollider>();
            inactive.SetActive(false);
            var child = _blockObject.AddComponent<BlockGameObjectChild>();
            child.Init(block);
            var collector = new RemovedObjectCollector();

            // 生成値欠落の本体とは独立に付随レールを採取する
            // Capture the attached rail even when the block's recreate state is missing
            LogAssert.Expect(LogType.Warning, new Regex("\\[RemovalRestore\\] unrecordable: block at"));
            child.CollectRemovedObjects(collector);
            Assert.AreEqual(1, collector.Objects.Count);
            Assert.AreEqual(1, collector.GetUnrecordableBlocks().Count);
            var sender = new FakeRemovalRestoreSender();
            collector.Objects[0].TrySendConnectionRestore(sender, new HashSet<Vector3Int>());
            CollectionAssert.AreEqual(new[] { $"rail:{origin}-{other}:{railType}" }, sender.Sent);
        }

        [Test]
        public void UnsyncedDestinationIsCountedAsUnrecordable()
        {
            var resolver = new BlockAttachedConnectionResolver(new ConnectionLineRegistry(), RailGraphClientCache.CreateForEditorTest());
            _blockObject = new GameObject("UnsyncedPier");
            var block = _blockObject.AddComponent<BlockGameObject>();
            SetBlockProperty(block, nameof(BlockGameObject.BlockPosInfo),
                new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            var area = new GameObject("Front", typeof(BoxCollider)).AddComponent<TrainRailConnectAreaCollider>();
            area.transform.SetParent(_blockObject.transform);
            area.isFront = true;
            area.Initialize(block);
            var collector = new RemovedObjectCollector();

            // 同じ未同期端点でも赤表示は情報、Undo採取は警告に分ける
            // Report the same unsynced endpoint as info in preview and warning in undo capture
            LogAssert.Expect(LogType.Log, new Regex("\\[RemovalPreview\\] rail node not synced yet:.*retry on next topology change"));
            resolver.RequestCascadePreview(block);
            LogAssert.Expect(LogType.Warning, new Regex("\\[RemovalRestore\\] unrecordable: rail at .*: node not synced"));
            resolver.CollectRemovedConnections(block, collector);
            Assert.IsEmpty(collector.Objects);
            Assert.AreEqual(1, collector.UnrecordableCount);
        }

        private static void UpsertPier(RailGraphClientCache cache, int frontNodeId, Vector3Int position)
        {
            var origin = (Vector3)position;
            cache.UpsertNode(frontNodeId, Guid.NewGuid(), origin, new ConnectionDestination(position, 0, true), origin + Vector3.forward, origin + Vector3.back);
            cache.UpsertNode(frontNodeId + 1, Guid.NewGuid(), origin, new ConnectionDestination(position, 0, false), origin + Vector3.back, origin + Vector3.forward);
        }

        private static void SetResolver(BlockAttachedConnectionResolver resolver)
        {
            typeof(ClientDIContext).GetProperty(nameof(ClientDIContext.BlockAttachedConnectionResolver))
                .GetSetMethod(true).Invoke(null, new object[] { resolver });
        }

        private static void SetBlockProperty(BlockGameObject block, string name, object value)
        {
            typeof(BlockGameObject).GetProperty(name).GetSetMethod(true).Invoke(block, new[] { value });
        }

        private sealed class Endpoints : IConnectionLineEndpointQuery
        {
            public bool TryGetPosition(BlockInstanceId instanceId, out Vector3Int position)
            {
                position = instanceId.AsPrimitive() == 1 ? Vector3Int.zero : Vector3Int.right;
                return true;
            }
        }

        private sealed class MissingEndpoints : IConnectionLineEndpointQuery
        {
            public bool TryGetPosition(BlockInstanceId instanceId, out Vector3Int position)
            {
                position = default;
                return false;
            }
        }
    }
}
