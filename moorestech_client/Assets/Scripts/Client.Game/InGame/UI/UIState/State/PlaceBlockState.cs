using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.Skit;
using Client.Input;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class PlaceBlockState : IUIState, IApplicationFocusRestorer
    {
        private readonly SkitManager _skitManager;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly List<IDisposable> _blockPlacedDisposable = new();
        private readonly PlaceSystemStateController _placeSystemStateController;
        private readonly PlacementTargetPickService _placementTargetPickService;
        private readonly IPlayerCameraInteractionApplier _cameraInteractionApplier;
        private readonly ReactiveProperty<int> _placementHeight = new(0);

        public IObservable<int> OnPlacementHeightChanged => _placementHeight;
        public int GetPlacementHeight() => _placementHeight.Value;

        public PlaceBlockState(SkitManager skitManager, BlockGameObjectDataStore blockGameObjectDataStore, PlaceSystemStateController placeSystemStateController, PlacementTargetPickService placementTargetPickService, IPlayerCameraInteractionApplier cameraInteractionApplier)
        {
            _skitManager = skitManager;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _placeSystemStateController = placeSystemStateController;
            _placementTargetPickService = placementTargetPickService;
            _cameraInteractionApplier = cameraInteractionApplier;
        }

        public void OnEnter(UITransitContext context)
        {
            _placementHeight.Value = 0;
            // 遷移payloadから設置ターゲットを受け取り所有者へ渡す（無ければEmptyに落ちる）
            // Take the placement target from the transition payload and hand it to the owner (falls back to Empty when absent)
            if (context.TryGetContext<IPlacementTarget>(out var target)) _placeSystemStateController.SetTarget(target);

            // 設置中は右ドラッグまで回転停止
            // Stop rotation until right-drag while placing
            _cameraInteractionApplier.SetCursorVisible(true);
            _cameraInteractionApplier.SetCameraRotatable(false);

            // ここが重くなったら近いブロックだけプレビューをオンにするなどする
            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.EnablePreviewOnlyObjects(true, true);
            }
            _blockPlacedDisposable.Add(_blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnPlaceBlock));

            KeyControlDescription.Instance.SetText("Tab: ブロック選択\nV: 視点切替\nQ: 設置高さ上げる\nE: ブロック高さ下げる\nB: 配置モード終了\n左クリック: ブロック配置\nG:ブロック削除\nミドルクリック: 設置物をスポイト");

            #region Internal

            void OnPlaceBlock(BlockGameObject blockGameObject)
            {
                blockGameObject.EnablePreviewOnlyObjects(true, false);

                _blockPlacedDisposable.Add(blockGameObject.OnFinishedPlaceAnimation.Subscribe(_ =>
                {
                    blockGameObject.EnablePreviewOnlyObjects(true, true);
                }));
            }

            #endregion
        }

        public UITransitContext GetNextUpdate()
        {
            if (_skitManager.IsPlayingSkit) return new UITransitContext(UIStateEnum.Story);

            // TabはOpenInventoryと同キーだが、配置モード中はビルドメニュー再表示を優先する
            // Tab shares the OpenInventory binding, but reopening the build menu takes precedence while placing
            if (HybridInput.GetKeyDown(KeyCode.Tab)) return new UITransitContext(UIStateEnum.BuildMenu);
            if (InputManager.UI.BlockDelete.GetKeyDown) return new UITransitContext(UIStateEnum.DeleteBar);
            if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.GameScreen);

            // 右ドラッグ中のみ設置照準回転
            // Rotate placement aim only during right-drag
            UpdateRightDragRotation();
            if (_placementTargetPickService.TryPickTargetUnderCursor(out var pickedTarget)) _placeSystemStateController.SetTarget(pickedTarget);

            _placeSystemStateController.ManualUpdate();

            // 実設置系と同じ入力でHUDの高さ表示を更新する
            // Update the HUD height from the same input used by placement systems
            if (HybridInput.GetKeyDown(KeyCode.Q)) _placementHeight.Value--;
            else if (HybridInput.GetKeyDown(KeyCode.E)) _placementHeight.Value++;

            return null;

            #region Internal

            void UpdateRightDragRotation()
            {
                if (HybridInput.GetMouseButtonDown(1))
                {
                    _cameraInteractionApplier.SetCursorVisible(false);
                    _cameraInteractionApplier.SetCameraRotatable(true);
                }

                if (!HybridInput.GetMouseButtonUp(1)) return;
                _cameraInteractionApplier.SetCursorVisible(true);
                _cameraInteractionApplier.SetCameraRotatable(false);
            }

            #endregion
        }

        public void OnExit()
        {
            _cameraInteractionApplier.SetCursorVisible(true);
            _cameraInteractionApplier.SetCameraRotatable(false);
            _placeSystemStateController.Disable();

            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.EnablePreviewOnlyObjects(false, false);
            }

            _blockPlacedDisposable.ForEach(d => d.Dispose());
            _blockPlacedDisposable.Clear();
        }

        public void RestoreAfterApplicationFocus()
        {
            _cameraInteractionApplier.SetCursorVisible(true);
            _cameraInteractionApplier.SetCameraRotatable(false);
        }
    }
}
