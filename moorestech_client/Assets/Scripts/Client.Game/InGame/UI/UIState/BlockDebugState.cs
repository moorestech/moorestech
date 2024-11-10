using System.Threading;
using Client.Game.GameDebug;
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
        
        public UIStateEnum GetNextUpdate()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (_skitManager.IsPlayingSkit) return UIStateEnum.Story;
            if (DebugInfoStore.EnableBlockDebugMode) return UIStateEnum.GameScreen;
            
            _screenClickableCameraController.GetNextUpdate();
            
            if (BlockClickDetect.TryGetCursorOnBlock(out var block))
            {
                DebugInfoStore.InvokeClickBlock(block);
            }
            
            return UIStateEnum.Current;
        }
        
        public void OnExit()
        {
            _screenClickableCameraController.OnExit();
        }
    }
}