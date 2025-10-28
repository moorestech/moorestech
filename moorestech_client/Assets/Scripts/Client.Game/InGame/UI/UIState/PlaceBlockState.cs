using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.Input;
using Client.Game.Skit;
using Client.Input;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class PlaceBlockState : IUIState
    {
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly ScreenClickableCameraController _screenClickableCameraController;
        private readonly SkitManager _skitManager;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly InGameCameraController _inGameCameraController;
        private readonly List<IDisposable> _blockPlacedDisposable = new();
        
        private bool _isChangeCameraAngle;
        
        public PlaceBlockState(IPlacementPreviewBlockGameObjectController previewBlockController, SkitManager skitManager, InGameCameraController inGameCameraController, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _skitManager = skitManager;
            _inGameCameraController = inGameCameraController;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _previewBlockController = previewBlockController;
            _screenClickableCameraController = new ScreenClickableCameraController(inGameCameraController);
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            BlockPlaceSystem.SetEnableBlockPlace(true);
            
            //TODO InputSystemのリファクタ対象
            // シフト+Bのときはカメラの位置を変えない
            // Shift+B does not change camera position
            _isChangeCameraAngle = !UnityEngine.Input.GetKey(KeyCode.LeftShift);
            _screenClickableCameraController.OnEnter(_isChangeCameraAngle);
            
            if (_isChangeCameraAngle)
            {
                // カメラの位置を保存しておく
                var topDown = _inGameCameraController.CreateTopDownTweenCameraInfo();
                _inGameCameraController.StartTweenCamera(topDown);
            }
            
            // ここが重くなったら近いブロックだけプレビューをオンにするなどする
            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.EnablePreviewOnlyObjects(true, true);
            }
            _blockPlacedDisposable.Add(_blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnPlaceBlock));
            
            KeyControlDescription.Instance.SetText("1~9: 設置ブロック選択\nQ: 設置高さ上げる\nE: ブロック高さ下げる\nB: 配置モード終了\n左クリック: ブロック配置\nG:ブロック削除");
        }
        
        public UIStateEnum GetNextUpdate()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (BlockClickDetect.IsClickOpenableBlock()) return UIStateEnum.BlockInventory;
            if (InputManager.UI.BlockDelete.GetKeyDown) return UIStateEnum.DeleteBar;
            if (_skitManager.IsPlayingSkit) return UIStateEnum.Story;
            //TODO InputSystemのリファクタ対象
            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.B)) return UIStateEnum.GameScreen;
            
            _screenClickableCameraController.GetNextUpdate();
            
            return UIStateEnum.Current;
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
            BlockPlaceSystem.SetEnableBlockPlace(false);
            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.EnablePreviewOnlyObjects(false, false);
            }
            
            _blockPlacedDisposable.ForEach(d => d.Dispose());
            _blockPlacedDisposable.Clear();
            _screenClickableCameraController.OnExit();
        }
    }
}