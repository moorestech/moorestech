using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class PlaceBlockState : IUIState
    {
        public PlaceBlockState()
        {
            Debug.Log("Create PlaceBlockState");
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            Debug.Log("Enter PlaceBlockState");
        }
        public UIStateEnum GetNext()
        {
            //TODO InputSystemのリファクタ対象
            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.B)) return UIStateEnum.GameScreen;
            
            return UIStateEnum.Current;
        }
        public void OnExit()
        {
            Debug.Log("Exit PlaceBlockState");
        }
    }
}