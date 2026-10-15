using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.View.Object.Material;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UIState
{
    public class ProductionPreviewResetKeyTest
    {
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _objects)
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
            _objects.Clear();
        }

        [Test]
        public void BlockChildrenShareParentViewKey()
        {
            var owner = CreateObject().AddComponent<BlockGameObject>();
            var first = CreateObject().AddComponent<BlockGameObjectChild>();
            var other = CreateObject().AddComponent<BlockGameObjectChild>();
            first.Init(owner);
            other.Init(owner);
            Assert.AreSame(owner, first.GetPreviewResetKey());
            Assert.AreSame(first.GetPreviewResetKey(), other.GetPreviewResetKey());
            AssertSharedFailure(first.GetPreviewResetKey(), other.GetPreviewResetKey());
        }

        [Test]
        public void TrainChildrenShareMaterialControllerKey()
        {
            var material = new TrainCarMaterialController(CreateObject());
            var first = CreateObject().AddComponent<TrainCarEntityChildrenObject>();
            var other = CreateObject().AddComponent<TrainCarEntityChildrenObject>();
            first.Initialize(null, material);
            other.Initialize(null, material);
            Assert.AreSame(material, first.GetPreviewResetKey());
            Assert.AreSame(first.GetPreviewResetKey(), other.GetPreviewResetKey());
            AssertSharedFailure(first.GetPreviewResetKey(), other.GetPreviewResetKey());
        }

        [Test]
        public void ConnectionLineOwnsItsViewKey()
        {
            var first = CreateObject().AddComponent<ConnectionLineDeleteTarget>();
            var other = CreateObject().AddComponent<ConnectionLineDeleteTarget>();
            Assert.AreSame(first, first.GetPreviewResetKey());
            Assert.AreNotSame(first.GetPreviewResetKey(), other.GetPreviewResetKey());
            AssertSharedFailure(first.GetPreviewResetKey(), first.GetPreviewResetKey());
        }

        [Test]
        public void RegeneratedRailWithSameEdgeCanPreviewAfterOldViewFailureAndDestruction()
        {
            var oldView = CreateObject().AddComponent<BezierRailChain>();
            var first = CreateRail(oldView);
            var other = CreateRail(oldView);
            Assert.AreSame(oldView, first.GetPreviewResetKey());
            Assert.AreSame(first.GetPreviewResetKey(), other.GetPreviewResetKey());
            var recovery = AssertSharedFailure(first.GetPreviewResetKey(), other.GetPreviewResetKey());

            // 同じedgeの新しい表示は旧表示の失敗記録に巻き込まれない
            // A new view of the same edge is independent of the failed old view
            var newView = CreateObject().AddComponent<BezierRailChain>();
            var regenerated = CreateRail(newView);
            Assert.AreEqual(first.GetDeleteTargetKey(), regenerated.GetDeleteTargetKey());
            Assert.AreNotSame(first.GetPreviewResetKey(), regenerated.GetPreviewResetKey());
            UnityEngine.Object.DestroyImmediate(oldView.gameObject);
            var healthy = new FakeDeleteTarget { Key = regenerated.GetDeleteTargetKey(), PreviewResetKey = regenerated.GetPreviewResetKey() };
            var hover = new SingleHoverDeletePreview(recovery);
            hover.Hover(healthy);
            Assert.AreSame(healthy, hover.Current);
            hover.Release();
            Assert.AreEqual(1, healthy.SetPreviewCount);
            Assert.AreEqual(1, healthy.ResetCount);
        }

        private DeleteTargetRail CreateRail(BezierRailChain owner)
        {
            var gameObject = CreateObject();
            gameObject.AddComponent<RailObjectIdCarrier>().SetRailObjectId(42);
            var target = gameObject.AddComponent<DeleteTargetRail>();
            target.SetParentBezierRailChain(owner);
            return target;
        }

        private GameObject CreateObject()
        {
            var gameObject = new GameObject("PreviewKeyTest");
            _objects.Add(gameObject);
            return gameObject;
        }

        private static DeletePreviewResetRecovery AssertSharedFailure(object firstKey, object otherKey)
        {
            // 本番のキーを使い、例外注入だけをテスト実装で行う
            // Use production keys with a test target solely for fault injection
            var recovery = new DeletePreviewResetRecovery();
            var preview = new SingleHoverDeletePreview(recovery);
            var first = new FakeDeleteTarget { PreviewResetKey = firstKey, ThrowOnReset = true };
            var other = new FakeDeleteTarget { PreviewResetKey = otherKey };
            preview.Hover(first);
            Assert.Throws<InvalidOperationException>(() => preview.Release());
            LogAssert.Expect(LogType.Warning, "[DragDelete] delete preview suppressed because the target's previous reset did not complete");
            preview.Hover(other);
            Assert.IsNull(preview.Current);
            Assert.AreEqual(0, other.SetPreviewCount);
            Assert.AreEqual(1, first.ResetCount);
            return recovery;
        }
    }
}
