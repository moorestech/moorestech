using System.Threading;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.UIState.Input;
using Client.Game.Skit;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class BlockDebugState : IUIState
    {
        private readonly SkitManager _skitManager;
        private readonly ScreenClickableCameraController _screenClickableCameraController;
        
        private CancellationTokenSource _startTweenCameraCancellationTokenSource;
        
        public BlockDebugState(SkitManager skitManager, InGameCameraController inGameCameraController)
        {
            _screenClickableCameraController = new ScreenClickableCameraController(inGameCameraController);
            _skitManager = skitManager;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _screenClickableCameraController.OnEnter();
            _screenClickableCameraController.StartTween();
        }
        
        public UIStateEnum GetNext()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (BlockClickDetect.IsClickOpenableBlock(_blockPlacePreview)) return UIStateEnum.BlockInventory;
            if (InputManager.UI.BlockDelete.GetKeyDown) return UIStateEnum.DeleteBar;
            if (_skitManager.IsPlayingSkit) return UIStateEnum.Story;
            //TODO InputSystemのリファクタ対象
            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.B)) return UIStateEnum.GameScreen;
            
            
            return UIStateEnum.Current;
        }
        
        public void OnExit()
        {
            _screenClickableCameraController.OnExit();
        }
    }
}