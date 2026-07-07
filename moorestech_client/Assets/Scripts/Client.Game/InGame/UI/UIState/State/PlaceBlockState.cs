using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control.BuildView;
using Client.Game.InGame.UI.KeyControl;
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

        public PlaceBlockState(SkitManager skitManager, BuildViewModeController buildViewModeController, BlockGameObjectDataStore blockGameObjectDataStore, PlaceSystemStateController placeSystemStateController)
        {
            _skitManager = skitManager;
            _buildViewModeController = buildViewModeController;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _placeSystemStateController = placeSystemStateController;
        }

        public void OnEnter(UITransitContext context)
        {
            // カメラ・カーソルの適用はBuildViewModeControllerに委譲する
            // Camera and cursor handling is delegated to BuildViewModeController
            _buildViewModeController.OnEnterBuildState(UIStateEnum.PlaceBlock);

            // ここが重くなったら近いブロックだけプレビューをオンにするなどする
            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.EnablePreviewOnlyObjects(true, true);
            }
            _blockPlacedDisposable.Add(_blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnPlaceBlock));

            KeyControlDescription.Instance.SetText("Tab: ブロック選択\nV: 視点切替\nQ: 設置高さ上げる\nE: ブロック高さ下げる\nB: 配置モード終了\n左クリック: ブロック配置\nG:ブロック削除");
        }

        public UITransitContext GetNextUpdate()
        {
            if (_skitManager.IsPlayingSkit) return Leave(UIStateEnum.Story);

            // 入力フィールド編集中はキー遷移と視点操作を止める（BP名入力中のB/Tab/V等の誤爆防止）
            // While a text field is edited, suppress key transitions and view input so BP naming (B/Tab/V etc.) can't trigger them
            if (!IsTextInputFocused())
            {
                // TabはOpenInventoryと同キーだが、配置モード中はビルドメニュー再表示を優先する
                // Tab shares the OpenInventory binding, but reopening the build menu takes precedence while placing
                if (HybridInput.GetKeyDown(KeyCode.Tab)) return Leave(UIStateEnum.BuildMenu);
                if (InputManager.UI.BlockDelete.GetKeyDown) return Leave(UIStateEnum.DeleteBar);
                //TODO InputSystem対応
                if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.B)) return Leave(UIStateEnum.GameScreen);

                _buildViewModeController.ManualUpdate();
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
