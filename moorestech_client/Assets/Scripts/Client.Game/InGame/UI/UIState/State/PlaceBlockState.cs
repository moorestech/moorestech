using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control.BuildView;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.BlockPick;
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
        private readonly BuildViewModeController _buildViewModeController;
        private readonly List<IDisposable> _blockPlacedDisposable = new();
        private readonly PlaceSystemStateController _placeSystemStateController;
        private readonly BlockPickService _blockPickService;
        private bool _wasTextInputFocused;

        public PlaceBlockState(SkitManager skitManager, BuildViewModeController buildViewModeController, BlockGameObjectDataStore blockGameObjectDataStore, PlaceSystemStateController placeSystemStateController, BlockPickService blockPickService)
        {
            _skitManager = skitManager;
            _buildViewModeController = buildViewModeController;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _placeSystemStateController = placeSystemStateController;
            _blockPickService = blockPickService;
        }

        public void OnEnter(UITransitContext context)
        {
            // カメラ・カーソルはBuildViewModeControllerへ委譲
            // Camera and cursor handling is delegated to BuildViewModeController
            _buildViewModeController.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _wasTextInputFocused = false;

            // ここが重くなったら近いブロックだけプレビューをオンにするなどする
            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.EnablePreviewOnlyObjects(true, true);
            }
            _blockPlacedDisposable.Add(_blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnPlaceBlock));

            KeyControlDescription.Instance.SetText("Tab: ブロック選択\nV: 視点切替\nQ: 設置高さ上げる\nE: ブロック高さ下げる\nB: 配置モード終了\n左クリック: ブロック配置\nG:ブロック削除\nミドルクリック: ブロックをスポイト");
        }

        public UITransitContext GetNextUpdate()
        {
            if (_skitManager.IsPlayingSkit) return Leave(UIStateEnum.Story);

            // フォーカス変化を視点コントローラへ通知（FPS中のダイアログでカーソルを解放するため）
            // Notify focus changes to the view controller so FPS dialogs can free the cursor
            var isTextInputFocused = IsTextInputFocused();
            if (isTextInputFocused != _wasTextInputFocused)
            {
                _wasTextInputFocused = isTextInputFocused;
                _buildViewModeController.SetTextInputFocused(isTextInputFocused);
            }

            // 入力フィールド編集中はキー遷移と視点操作を止める（BP名入力中のB/Tab/V等の誤爆防止）
            // While a text field is edited, suppress key transitions and view input so BP naming (B/Tab/V etc.) can't trigger them
            if (!isTextInputFocused)
            {
                // TabはOpenInventoryと同キーだが、配置モード中はビルドメニュー再表示を優先する
                // Tab shares the OpenInventory binding, but reopening the build menu takes precedence while placing
                if (HybridInput.GetKeyDown(KeyCode.Tab)) return Leave(UIStateEnum.BuildMenu);
                if (InputManager.UI.BlockDelete.GetKeyDown) return Leave(UIStateEnum.DeleteBar);
                //TODO InputSystem対応
                if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.B)) return Leave(UIStateEnum.GameScreen);

                _buildViewModeController.ManualUpdate();

                // ミドルクリックで選択ブロックをスポイトで切り替える
                // Middle-click switches the selected block via the eyedropper
                _blockPickService.TryPickBlockUnderCursor();
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

        // 遷移確定をコントローラへ通知してから遷移する（セッション終了判定はコントローラ側）
        // Notify the controller before transiting; it decides whether the session ends
        private UITransitContext Leave(UIStateEnum next)
        {
            _buildViewModeController.OnLeaveBuildState(next);
            return new UITransitContext(next);
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
