using System;
using System.Text.RegularExpressions;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
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

        private BlockGameObject CreatePierBlock(Vector3Int origin)
        {
            _blockObject = new GameObject("Pier");
            var block = _blockObject.AddComponent<BlockGameObject>();
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).GetSetMethod(true)
                .Invoke(block, new object[] { new BlockPositionInfo(origin, BlockDirection.North, Vector3Int.one) });
            var area = new GameObject("Front", typeof(BoxCollider)).AddComponent<TrainRailConnectAreaCollider>();
            area.transform.SetParent(_blockObject.transform);
            area.isFront = true;
            area.Initialize(block);
            return block;
        }

        private static void UpsertPier(RailGraphClientCache cache, int frontNodeId, Vector3Int position)
        {
            var origin = (Vector3)position;
            cache.UpsertNode(frontNodeId, Guid.NewGuid(), origin, new ConnectionDestination(position, 0, true), origin + Vector3.forward, origin + Vector3.back);
            cache.UpsertNode(frontNodeId + 1, Guid.NewGuid(), origin, new ConnectionDestination(position, 0, false), origin + Vector3.back, origin + Vector3.forward);
        }
    }
}
