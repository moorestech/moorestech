using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class BuildMenuState : IUIState
    {
        private readonly BuildMenuView _buildMenuView;
        private readonly PlayerViewModeController _playerViewModeController;

        public BuildMenuState(BuildMenuView buildMenuView, PlayerViewModeController playerViewModeController)
        {
            _buildMenuView = buildMenuView;
            _playerViewModeController = playerViewModeController;
        }

        public void OnEnter(UITransitContext context)
        {
            // カーソル適用はPlayerViewModeController委譲（FPS中も解放）
            // Cursor visibility is applied by PlayerViewModeController (freed in the menu even during FPS)
            _playerViewModeController.OnEnterViewState(UIStateEnum.BuildMenu);
            _buildMenuView.SetActive(true);
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
            // 視点回転を落とす。カーソル方針は次ステートのOnEnterが適用する
            // Drop the look rotation; the next state's OnEnter applies its own cursor policy
            _playerViewModeController.OnExitViewState();
            _buildMenuView.SetActive(false);
        }
    }
}
