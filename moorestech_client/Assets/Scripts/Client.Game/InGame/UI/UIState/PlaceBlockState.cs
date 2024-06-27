using System.Threading;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control;
using Client.Game.Skit;
using Client.Input;
using Cysharp.Threading.Tasks;
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
        
        private CancellationTokenSource _startTweenCameraCancellationTokenSource;
        
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
            
            _startCameraDistance = _inGameCameraController.CameraDistance;
            _startCameraRotation = _inGameCameraController.CameraEulerAngle;
            
            
            
            #region Internal
            
            async UniTask TweenCamera()
            {
                var currentRotation = _inGameCameraController.CameraEulerAngle;
                var targetCameraRotation = currentRotation;
                targetCameraRotation.x = 70f;
                targetCameraRotation.y = currentRotation.y switch
                {
                    var y when y < 45 => 0,
                    var y when y < 135 => 90,
                    var y when y < 225 => 180,
                    var y when y < 315 => 270,
                    _ => 0
                };
                _inGameCameraController.StartTweenCamera(targetCameraRotation, TargetCameraDistance, TweenDuration);
            }
            
            #endregion
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
                _inGameCameraController.SetControllable(true);
            }
            
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetMouseButtonUp(1))
            {
                InputManager.MouseCursorVisible(true);
                _inGameCameraController.SetControllable(false);
            }
            
            return UIStateEnum.Current;
        }
        
        public void OnExit()
        {
            InputManager.MouseCursorVisible(false);
            
            // カメラの位置がターゲットからあまり変わっていなければ元の座標に戻す
            var isResetDistance = Mathf.Abs(_inGameCameraController.CameraDistance - TargetCameraDistance) < 3f;
            
            var isResetXRotation = Quaternion.Angle(Quaternion.Euler(_inGameCameraController.CameraEulerAngle.x,0,0), Quaternion.Euler(_targetCameraRotation.x,0,0)) < 10f;
            var isResetYRotation = Quaternion.Angle(Quaternion.Euler(0,_inGameCameraController.CameraEulerAngle.y,0), Quaternion.Euler(0,_targetCameraRotation.y,0)) < 10f;
            
            if (isResetDistance || isResetXRotation || isResetYRotation)
            {
                var targetRotation =  _inGameCameraController.CameraEulerAngle;
                targetRotation.x = isResetXRotation ? _startCameraRotation.x : targetRotation.x;
                targetRotation.y = isResetYRotation ? _startCameraRotation.y : targetRotation.y;
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