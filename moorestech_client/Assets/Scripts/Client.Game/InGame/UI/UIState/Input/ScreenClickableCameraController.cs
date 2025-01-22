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
        
        public const float DefaultTweenDuration = 0.25f;
        
        private TweenCameraInfo _startCameraTweenInfo;
        
        public ScreenClickableCameraController(InGameCameraController inGameCameraController)
        {
            _inGameCameraController = inGameCameraController;
        }
        
        public void OnEnter(bool saveCurrentCamera)
        {
            if (saveCurrentCamera)
            {
                _startCameraTweenInfo = _inGameCameraController.CreateCurrentCameraTweenCameraInfo();
            }
            
            InputManager.MouseCursorVisible(true);
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
            if (_startCameraTweenInfo != null)
            {
                _inGameCameraController.StartTweenCamera(_startCameraTweenInfo);
            }
            
            InputManager.MouseCursorVisible(false);
        }
    }
    
    public class TweenCameraInfo
    {
        public readonly Vector3 Rotation;
        public readonly float Distance;
        public readonly float TweenDuration;
        
        public TweenCameraInfo(Vector3 rotation, float distance, float tweenDuration = ScreenClickableCameraController.DefaultTweenDuration)
        {
            Rotation = rotation;
            Distance = distance;
            TweenDuration = tweenDuration;
        }
    }
}