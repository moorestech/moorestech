using Client.Game.InGame.Control;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Game.Skit;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class GameScreenState : IUIState
    {
        private readonly InGameCameraController _inGameCameraController;
        private readonly SkitManager _skitManager;
        private readonly GameScreenSubInventoryInteractService _subInventoryInteractService;
        
        public GameScreenState(SkitManager skitManager, InGameCameraController inGameCameraController, GameScreenSubInventoryInteractService subInventoryInteractService)
        {
            _skitManager = skitManager;
            _inGameCameraController = inGameCameraController;
            _subInventoryInteractService = subInventoryInteractService;
        }
        
        public UITransitContext GetNextUpdate()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);
            if (InputManager.UI.OpenMenu.GetKeyDown) return new UITransitContext(UIStateEnum.PauseMenu);
            
            // ブロックや列車とインタラクトしたか
            if (_subInventoryInteractService.TryGetSubInventoryInteractObject(out var context)) return context;

            if (InputManager.UI.BlockDelete.GetKeyDown) return new UITransitContext(UIStateEnum.DeleteBar);
            if (_skitManager.IsPlayingSkit) return new UITransitContext(UIStateEnum.Story);
            
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.PlaceBlock);
            if (UnityEngine.Input.GetKeyDown(KeyCode.T)) return new UITransitContext(UIStateEnum.ChallengeList);
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) return new UITransitContext(UIStateEnum.ResearchTree);
            
            return null;
        }

        public void OnEnter(UITransitContext context)
        {
            InputManager.MouseCursorVisible(false);
            _inGameCameraController.SetControllable(true);

            KeyControlDescription.Instance.SetText("Tab: Inventory\n1~9: Switch items\nB: Place block\nG: Delete block\nT: Challenge list\nR: Research tree\n");
        }
        
        public void OnExit()
        {
            _inGameCameraController.SetControllable(false);
        }
    }
}
