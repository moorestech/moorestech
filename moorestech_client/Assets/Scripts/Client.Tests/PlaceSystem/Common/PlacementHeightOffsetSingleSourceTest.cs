using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Empty;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Tests.UIState.Fakes;
using Client.Ui.State.ModeHud;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common
{
    /// <summary>
    ///     実値と UITK の HUD の表示モデルが同一ソースであることを検証
    ///     Verifies the actual height and the UITK HUD view model share one source
    /// </summary>
    public class PlacementHeightOffsetSingleSourceTest
    {
        private GameObject _controlObject;

        [TearDown]
        public void TearDown()
        {
            if (_controlObject != null) UnityEngine.Object.DestroyImmediate(_controlObject);
        }

        [Test]
        public void ドラッグ中の高さ変更は解放で開始値へ戻り表示も同じ値になる()
        {
            var heightOffset = new PlacementHeightOffset();
            var dragState = new CommonBlockPlaceDragState(heightOffset);
            var hud = new ModeHudModel(CreatePlaceBlockUiState(), CreateController(heightOffset), heightOffset);
            hud.Initialize();

            dragState.BeginDrag(Vector3Int.zero, PlacementHitSurfaceKind.Ground);
            heightOffset.Adjust(2);

            // 表示モデルの高さが源と設置の高さに一致する
            // The view model height matches the source and the placement height
            Assert.AreEqual(2, ((ModeHudPlacement)hud.Current.Value).Height, "the in-drag height did not reach the HUD view model");
            Assert.AreEqual(heightOffset.Value, ((ModeHudPlacement)hud.Current.Value).Height);
            Assert.AreEqual(dragState.HeightOffset, ((ModeHudPlacement)hud.Current.Value).Height);

            dragState.EndDrag();

            Assert.AreEqual(0, dragState.HeightOffset);
            Assert.AreEqual(0, ((ModeHudPlacement)hud.Current.Value).Height, "the HUD kept the in-drag height after the drag restored it");
        }

        [Test]
        public void 持ち替えで高さが地表基準へ戻ると表示も戻る()
        {
            var heightOffset = new PlacementHeightOffset();
            var firstTarget = new BlueprintPlacementTarget(Guid.NewGuid(), "first", new global::Game.Blueprint.BlueprintJsonObject());
            var dragState = new CommonBlockPlaceDragState(heightOffset);
            heightOffset.SyncSelectedTarget(firstTarget);
            heightOffset.Adjust(3);

            heightOffset.SyncSelectedTarget(new BlueprintPlacementTarget(Guid.NewGuid(), "second", new global::Game.Blueprint.BlueprintJsonObject()));

            Assert.AreEqual(0, heightOffset.Value, "the HUD kept the old height after a hotbar swap reset the actual one");
        }

        [Test]
        public void ドラッグ中に設置系が畳まれても高さは開始値へ戻る()
        {
            var heightOffset = new PlacementHeightOffset();
            var firstTarget = new BlueprintPlacementTarget(Guid.NewGuid(), "first", new global::Game.Blueprint.BlueprintJsonObject());
            var dragState = new CommonBlockPlaceDragState(heightOffset);
            heightOffset.SyncSelectedTarget(firstTarget);
            dragState.BeginDrag(Vector3Int.zero, PlacementHitSurfaceKind.Ground);
            heightOffset.Adjust(2);

            // ドラッグ途中の離脱（Tab等）でも高さはドラッグ開始値へ戻す。解放時と同じ規則
            // Leaving mid-drag (Tab and friends) returns the height to the drag's starting value, as a release does
            dragState.ClearDrag();

            Assert.AreEqual(0, heightOffset.Value, "the temporary in-drag height survived a teardown");
        }

        [Test]
        public void 共有の正を渡した2つのドラッグ状態は同じ高さを読み書きする()
        {
            var heightOffset = new PlacementHeightOffset();
            var firstTarget = new BlueprintPlacementTarget(Guid.NewGuid(), "first", new global::Game.Blueprint.BlueprintJsonObject());
            var firstDragState = new CommonBlockPlaceDragState(heightOffset);
            var secondDragState = new CommonBlockPlaceDragState(heightOffset);

            heightOffset.SyncSelectedTarget(firstTarget);
            heightOffset.Adjust(1);
            Assert.AreEqual(1, secondDragState.HeightOffset);

            heightOffset.Adjust(2);
            Assert.AreEqual(3, firstDragState.HeightOffset);

            // 持ち替え判定も1本。片側で持ち替えたらもう片側から見た高さも地表基準へ戻る
            // The block-switch check is shared too, so a swap on one side returns the other side's height to ground
            heightOffset.SyncSelectedTarget(new BlueprintPlacementTarget(Guid.NewGuid(), "second", new global::Game.Blueprint.BlueprintJsonObject()));
            Assert.AreEqual(0, firstDragState.HeightOffset);
        }

        private static PlaceSystemStateController CreateController(PlacementHeightOffset heightOffset)
        {
            return new PlaceSystemStateController(new EmptySelector(), new NullPresenter(), heightOffset);
        }

        // 設置モードを初期状態にした UIStateControl（前例 UIStateControlTest の組み方）
        // A UIStateControl starting in placement mode (built like the UIStateControlTest precedent)
        private UIStateControl CreatePlaceBlockUiState()
        {
            var dictionary = new UIStateDictionary(null, null, null, null, null, null, null, null, null, null, null, null);
            var field = typeof(UIStateDictionary).GetField("_stateDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
            var states = (Dictionary<UIStateEnum, IUIState>)field.GetValue(dictionary);
            states[UIStateEnum.PlaceBlock] = new IdleUIState();

            _controlObject = new GameObject("UIStateControl");
            var control = _controlObject.AddComponent<UIStateControl>();
            control.Construct(dictionary, new FakePlayerObjectController());
            control.Initialize(UIStateEnum.PlaceBlock, new UITransitContext(UIStateEnum.PlaceBlock));
            return control;
        }

        // ManualUpdateを一切駆動しないため、EmptyPlaceSystemが読まれるだけの空セレクタ
        // ManualUpdate is never driven here, so this only backs the EmptyPlaceSystem read
        private class EmptySelector : IPlaceSystemSelector
        {
            public IPlaceSystem EmptyPlaceSystem { get; } = new EmptyPlaceSystem();
            public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context) => EmptyPlaceSystem;
        }

        // 遷移せず何もしない設置モードの状態
        // A placement-mode state that never transitions and does nothing
        private class IdleUIState : IUIState
        {
            public void OnEnter(UITransitContext context) { }
            public UITransitContext GetNextUpdate() => null;
            public void OnExit() { }
            public bool LocksPlayerMovement() => false;
            public bool AllowsSprintToggle() => true;
            public IReadOnlyList<KeyHint> GetKeyHints() => Array.Empty<KeyHint>();
        }

        private class NullPresenter : IPlacementFeedbackPresenter
        {
            public void Present(PlacementFeedback feedback) { }
            public void Hide() { }
        }
    }
}
