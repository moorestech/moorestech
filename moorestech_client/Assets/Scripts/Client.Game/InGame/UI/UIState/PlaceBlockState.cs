using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Control;
using Client.Game.Skit;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class PlaceBlockState : IUIState
    {
        private readonly IBlockPlacePreview _blockPlacePreview;
        private readonly InGameCameraController _inGameCameraController;
        private readonly SkitManager _skitManager;
        
        private Vector3 _startCameraRotation;
        private float _startCameraDistance;
        
        private Vector3 _targetCameraRotation;
        
        private const float TargetCameraDistance = 9;
        private const float TweenDuration = 0.25f;
        
        public PlaceBlockState(IBlockPlacePreview blockPlacePreview, SkitManager skitManager, InGameCameraController inGameCameraController)
        {
            _skitManager = skitManager;
            _blockPlacePreview = blockPlacePreview;
            _inGameCameraController = inGameCameraController;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            InputManager.MouseCursorVisible(true);
            var currentRotation = _inGameCameraController.CameraEulerAngle;
            _startCameraRotation = currentRotation;
            _targetCameraRotation = currentRotation;
            _startCameraDistance = _inGameCameraController.CameraDistance;
            
            _targetCameraRotation.x = 80f;
            _targetCameraRotation.y = 0f;
            _inGameCameraController.StartTweenCamera(_targetCameraRotation, TargetCameraDistance, TweenDuration);
        }
        
        public UIStateEnum GetNext()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (IsClickOpenableBlock()) return UIStateEnum.BlockInventory;
            if (InputManager.UI.BlockDelete.GetKeyDown) return UIStateEnum.DeleteBar;
            if (_skitManager.IsPlayingSkit) return UIStateEnum.Story;
            //TODO InputSystemのリファクタ対象
            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.B)) return UIStateEnum.GameScreen;
            
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                InputManager.MouseCursorVisible(false);
                _inGameCameraController.SetUpdateCameraAngle(true);
            }
            
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetMouseButtonUp(1))
            {
                InputManager.MouseCursorVisible(true);
                _inGameCameraController.SetUpdateCameraAngle(false);
            }
            
            return UIStateEnum.Current;
        }
        
        public void OnExit()
        {
            InputManager.MouseCursorVisible(false);
            
            // カメラの位置がターゲットからあまり変わっていなければ元の座標に戻す
            var isResetDistance = Mathf.Abs(_inGameCameraController.CameraDistance - TargetCameraDistance) < 1f;
            var isResetRotation = Quaternion.Angle(Quaternion.Euler(_inGameCameraController.CameraEulerAngle), Quaternion.Euler(_targetCameraRotation)) < 25f;
            
            if (isResetDistance || isResetRotation)
            {
                var targetRotation = isResetRotation ? _startCameraRotation : _inGameCameraController.CameraEulerAngle;
                var targetDistance = isResetDistance ? _startCameraDistance : _inGameCameraController.CameraDistance;
                
                _inGameCameraController.StartTweenCamera(targetRotation, targetDistance, TweenDuration);
            }
        }
        
        private bool IsClickOpenableBlock()
        {
            if (_blockPlacePreview.IsActive) return false; //ブロック設置中の場合は無効
            if (BlockClickDetect.TryGetClickBlock(out var block)) return block.GetComponent<OpenableInventoryBlock>();
            
            return false;
        }
    }
}