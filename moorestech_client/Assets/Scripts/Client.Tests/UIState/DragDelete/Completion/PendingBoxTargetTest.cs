using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.State.DragDelete.Box;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using Client.Tests.BuildUndo;
using Client.Tests.UIState.Fakes;
using Game.Train.Unit;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UIState.DragDelete.Completion
{
    public class PendingBoxTargetTest
    {
        private GameObject _root;
        private TrainRailObjectManager _previousManager;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            typeof(TrainRailObjectManager).GetProperty(nameof(TrainRailObjectManager.Instance)).SetValue(null, _previousManager);
        }

        [TestCase(DeleteCompletionKind.Pending)]
        [TestCase(DeleteCompletionKind.Unconfirmed)]
        public void PendingOnlyBoxHasZeroCandidatesAndHoverCannotStartHold(DeleteCompletionKind kind)
        {
            var root = _root = new GameObject("pending-box-test");
            _previousManager = TrainRailObjectManager.Instance;
            var blocks = root.AddComponent<BlockGameObjectDataStore>();
            var cars = root.AddComponent<TrainCarObjectDatastore>();
            // EditModeではAwakeが走らないため、既存sceneの初期化条件を再現する
            // EditMode skips Awake, so reproduce the initialized scene dependency
            typeof(TrainRailObjectManager).GetProperty(nameof(TrainRailObjectManager.Instance)).SetValue(null, root.AddComponent<TrainRailObjectManager>());
            var car = root.AddComponent<TrainCarEntityObject>();
            var child = root.AddComponent<TrainCarEntityChildrenObject>();
            child.Initialize(car, null);
            var entities = (Dictionary<TrainCarInstanceId, TrainCarEntityObject>)typeof(TrainCarObjectDatastore).GetField("_entities", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cars);
            entities.Add(car.TrainCarInstanceId, car);
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
            var pending = new FakeDeleteTarget { Key = car.TrainCarInstanceId, Removable = true, CompleteImmediately = false };
            selection.AddBoxTargets(new[] { pending });
            selection.CommitDelete();
            pending.Complete(kind, null);

            // 実resolverの箱件数とモデルの追加/ホバーが同じ保留規則を使う
            // The real resolver count and model addition/hover share the pending rule
            var resolver = new DeleteBoxTargetResolver(blocks, cars);
            Assert.AreEqual(0, resolver.Resolve(Vector3Int.zero, Vector3Int.one, selection).Count);
            Assert.AreEqual(0, selection.AddBoxTargets(new[] { pending }));
            Assert.AreEqual(DeleteHoldSubjectKind.None, selection.UpdateHoverPreview(pending, out var reason).Kind);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.DeleteRequestPending, reason);
            pending.Complete(DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.InventoryFull);
            Assert.AreEqual(1, resolver.Resolve(Vector3Int.zero, Vector3Int.one, selection).Count);
            Assert.AreEqual(DeleteHoldSubjectKind.Hovered, selection.UpdateHoverPreview(pending, out _).Kind);
        }
    }
}
