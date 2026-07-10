using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Control.BuildView;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class BuildMenuState : IUIState
    {
        private readonly BuildMenuView _buildMenuView;
        private readonly BuildViewModeController _buildViewModeController;

        public BuildMenuState(BuildMenuView buildMenuView, BuildViewModeController buildViewModeController)
        {
            _buildMenuView = buildMenuView;
            _buildViewModeController = buildViewModeController;
        }

        public void OnEnter(UITransitContext context)
        {
            // カーソル適用はBuildViewModeController委譲（FPS中も解放）
            // Cursor visibility is applied by BuildViewModeController (freed in the menu even during FPS)
            _buildViewModeController.OnEnterBuildState(UIStateEnum.BuildMenu);
            _buildMenuView.SetActive(true);
            KeyControlDescription.Instance.SetText("クリック: 設置ブロック選択  B: 閉じる");
        }

        public UITransitContext GetNextUpdate()
        {
            if (_buildMenuView.TryConsumeSelectedEntry(out var entry))
                return Leave(UIStateEnum.PlaceBlock, UITransitContextContainer.Create<IPlacementTarget>(entry.Target));

            if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.B)) return Leave(UIStateEnum.GameScreen, null);
            if (InputManager.UI.OpenInventory.GetKeyDown) return Leave(UIStateEnum.PlayerInventory, null);

            return null;
        }

        // 遷移確定をコントローラへ通知してから遷移する（セッション終了判定はコントローラ側）
        // Notify the controller before transiting; it decides whether the session ends
        private UITransitContext Leave(UIStateEnum next, UITransitContextContainer container)
        {
            _buildViewModeController.OnLeaveBuildState(next);
            return new UITransitContext(next, container);
        }

        public void OnExit()
        {
            _buildMenuView.SetActive(false);
        }
    }
}
