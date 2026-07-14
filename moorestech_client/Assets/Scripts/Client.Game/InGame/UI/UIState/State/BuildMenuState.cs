using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class BuildMenuState : IUIState
    {
        private readonly BuildMenuView _buildMenuView;
        private readonly PlayerCameraInteractionController _cameraInteractionController;

        public BuildMenuState(BuildMenuView buildMenuView, InGameCameraController inGameCameraController)
        {
            _buildMenuView = buildMenuView;
            _cameraInteractionController = new PlayerCameraInteractionController(inGameCameraController);
        }

        public void OnEnter(UITransitContext context)
        {
            _buildMenuView.SetActive(true);

            // メニュー操作用にカーソルを解放し、視点回転を停止する
            // Release the cursor and stop look rotation for menu interaction
            _cameraInteractionController.EnterCursorInteraction();
            KeyControlDescription.Instance.SetText("クリック: 設置ブロック選択  B: 閉じる");
        }

        public UITransitContext GetNextUpdate()
        {
            if (_buildMenuView.TryConsumeSelectedEntry(out var entry))
                return new UITransitContext(UIStateEnum.PlaceBlock, UITransitContextContainer.Create<IPlacementTarget>(entry.Target));

            if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.GameScreen, null);
            if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory, null);

            return null;
        }

        public void OnExit()
        {
            _buildMenuView.SetActive(false);
        }
    }
}
