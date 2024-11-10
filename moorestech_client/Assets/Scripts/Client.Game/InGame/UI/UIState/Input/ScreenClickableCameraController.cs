using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.Input
{
    /// <summary>
    /// 画面上の要素をクリックできるようなとき（ブロック設置など）のカメラ操作
    /// Camera operation when elements on the screen can be clicked (e.g., block placement)
    /// </summary>
    public class ScreenClickableCameraController
    {
        private readonly InGameCameraController _inGameCameraController;
        
        private const float TargetCameraDistance = 9;
        private const float TweenDuration = 0.25f;
        
        private Vector3? _startCameraRotation;
        private float? _startCameraDistance;
        
        public ScreenClickableCameraController(InGameCameraController inGameCameraController)
        {
            _inGameCameraController = inGameCameraController;
        }
        
        public void OnEnter()
        {
            InputManager.MouseCursorVisible(true);
            BlockPlaceSystem.SetEnableBlockPlace(true);
        }
        
        public void GetNextUpdate()
        {
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
        }
        
        public void OnExit()
        {
            if (_startCameraRotation.HasValue && _startCameraDistance.HasValue)
            {
                var startCameraRotation = _startCameraRotation.Value;
                var startCameraDistance = _startCameraDistance.Value;
                _inGameCameraController.StartTweenCamera(startCameraRotation, startCameraDistance, TweenDuration);
            }
            
            InputManager.MouseCursorVisible(false);
            BlockPlaceSystem.SetEnableBlockPlace(false);
        }
        
        /// <summary>
        /// 上からのビューにカメラを移動させる
        /// Move the camera to a top view
        /// </summary>
        public void StartTween()
        {
            _startCameraDistance = _inGameCameraController.CameraDistance;
            _startCameraRotation = _inGameCameraController.CameraEulerAngle;
            
            TweenCamera();
            
            #region Internal
            
            void TweenCamera()
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
    }
}