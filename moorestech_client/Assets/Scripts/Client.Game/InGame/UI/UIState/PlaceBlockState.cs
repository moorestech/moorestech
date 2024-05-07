using Client.Game.InGame.Control;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class PlaceBlockState : IUIState
    {
        private readonly InGameCameraController _inGameCameraController;
        
        public PlaceBlockState(InGameCameraController inGameCameraController)
        {
            _inGameCameraController = inGameCameraController;
            Debug.Log("Create PlaceBlockState");
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            Debug.Log("Enter PlaceBlockState");
            InputManager.MouseCursorVisible(true);
        }
        public UIStateEnum GetNext()
        {
            //TODO InputSystemのリファクタ対象
            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.B)) return UIStateEnum.GameScreen;
            
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                InputManager.MouseCursorVisible(false);
                _inGameCameraController.updateCameraAngle = true;
            }
            if (UnityEngine.Input.GetMouseButtonUp(1))
            {
                InputManager.MouseCursorVisible(true);
                _inGameCameraController.updateCameraAngle = false;
            }
            
            return UIStateEnum.Current;
        }
        public void OnExit()
        {
            Debug.Log("Exit PlaceBlockState");
            InputManager.MouseCursorVisible(false);
        }
    }
}