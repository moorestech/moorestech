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
            if (InputManager.UI.OpenInventory.GetKeyDown) return Leave(UIStateEnum.PlayerInventory);
            if (InputManager.UI.BlockDelete.GetKeyDown) return Leave(UIStateEnum.DeleteBar);
            if (_skitManager.IsPlayingSkit) return Leave(UIStateEnum.Story);
            // Tabでビルドメニューを開き直す
            // Reopen the build menu with Tab
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) return Leave(UIStateEnum.BuildMenu);
            //TODO InputSystemのリファクタ対象
            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.B)) return Leave(UIStateEnum.GameScreen);

            _buildViewModeController.ManualUpdate();
            _placeSystemStateController.ManualUpdate();

            return null;
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
