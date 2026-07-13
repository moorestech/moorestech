using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.Skit;
using Client.Input;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class PlaceBlockState : IUIState
    {
        private readonly SkitManager _skitManager;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly PlayerViewModeController _playerViewModeController;
        private readonly List<IDisposable> _blockPlacedDisposable = new();
        private readonly PlaceSystemStateController _placeSystemStateController;
        private readonly PlacementTargetPickService _placementTargetPickService;
        private bool _wasTextInputFocused;

        public PlaceBlockState(SkitManager skitManager, PlayerViewModeController playerViewModeController, BlockGameObjectDataStore blockGameObjectDataStore, PlaceSystemStateController placeSystemStateController, PlacementTargetPickService placementTargetPickService)
        {
            _skitManager = skitManager;
            _playerViewModeController = playerViewModeController;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _placeSystemStateController = placeSystemStateController;
            _placementTargetPickService = placementTargetPickService;
        }

        public void OnEnter(UITransitContext context)
        {
            // 遷移payloadから設置ターゲットを受け取り所有者へ渡す（無ければEmptyに落ちる）
            // Take the placement target from the transition payload and hand it to the owner (falls back to Empty when absent)
            if (context.TryGetContext<IPlacementTarget>(out var target)) _placeSystemStateController.SetTarget(target);

            // カメラ・カーソルはPlayerViewModeControllerへ委譲
            // Camera and cursor handling is delegated to PlayerViewModeController
            _playerViewModeController.OnEnterViewState(UIStateEnum.PlaceBlock);
            _wasTextInputFocused = false;

            // ここが重くなったら近いブロックだけプレビューをオンにするなどする
            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.EnablePreviewOnlyObjects(true, true);
            }
            _blockPlacedDisposable.Add(_blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnPlaceBlock));

            KeyControlDescription.Instance.SetText("Tab: ブロック選択\nV: 視点切替\nQ: 設置高さ上げる\nE: ブロック高さ下げる\nB: 配置モード終了\n左クリック: ブロック配置\nG:ブロック削除\nミドルクリック: 設置物をスポイト");
        }

        public UITransitContext GetNextUpdate()
        {
            if (_skitManager.IsPlayingSkit) return new UITransitContext(UIStateEnum.Story);

            // フォーカス変化を視点コントローラへ通知（FPS中のダイアログでカーソルを解放するため）
            // Notify focus changes to the view controller so FPS dialogs can free the cursor
            var isTextInputFocused = IsTextInputFocused();
            if (isTextInputFocused != _wasTextInputFocused)
            {
                _wasTextInputFocused = isTextInputFocused;
                _playerViewModeController.SetTextInputFocused(isTextInputFocused);
            }

            // 入力フィールド編集中はキー遷移と視点操作を止める（BP名入力中のB/Tab/V等の誤爆防止）
            // While a text field is edited, suppress key transitions and view input so BP naming (B/Tab/V etc.) can't trigger them
            if (!isTextInputFocused)
            {
                // TabはOpenInventoryと同キーだが、配置モード中はビルドメニュー再表示を優先する
                // Tab shares the OpenInventory binding, but reopening the build menu takes precedence while placing
                if (HybridInput.GetKeyDown(KeyCode.Tab)) return new UITransitContext(UIStateEnum.BuildMenu);
                if (InputManager.UI.BlockDelete.GetKeyDown) return new UITransitContext(UIStateEnum.DeleteBar);
                //TODO InputSystem対応
                if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.GameScreen);

                _playerViewModeController.ManualUpdate();

                // ミドルクリックのスポイトで設置対象を持ち替える
                // Middle-click eyedropper swaps the current placement target
                if (_placementTargetPickService.TryPickTargetUnderCursor(out var pickedTarget)) _placeSystemStateController.SetTarget(pickedTarget);
            }

            _placeSystemStateController.ManualUpdate();

            return null;

            #region Internal

            // 選択中のTMP_InputFieldが編集中かを判定する
            // Whether the currently selected TMP_InputField is being edited
            static bool IsTextInputFocused()
            {
                var selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
                return selected != null && selected.TryGetComponent<TMPro.TMP_InputField>(out var inputField) && inputField.isFocused;
            }

            #endregion
        }

        private void OnPlaceBlock(BlockGameObject blockGameObject)
        {
            blockGameObject.EnablePreviewOnlyObjects(true, false);

            _blockPlacedDisposable.Add(blockGameObject.OnFinishedPlaceAnimation.Subscribe(_ =>
            {
                blockGameObject.EnablePreviewOnlyObjects(true, true);
            }));
        }

        public void OnExit()
        {
            // クロスヘアと視点回転を落とす。カーソル方針は次ステートのOnEnterが適用する
            // Drop the crosshair and look rotation; the next state's OnEnter applies its own cursor policy
            _playerViewModeController.OnExitViewState();
            _placeSystemStateController.Disable();

            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.EnablePreviewOnlyObjects(false, false);
            }

            _blockPlacedDisposable.ForEach(d => d.Dispose());
            _blockPlacedDisposable.Clear();
        }
    }
}
