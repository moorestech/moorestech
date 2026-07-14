using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class BuildMenuState : IUIState, IApplicationFocusRestorer
    {
        private readonly IBuildMenuView _buildMenuView;
        private readonly IPlayerCameraInteractionApplier _cameraInteractionApplier;

        public BuildMenuState(IBuildMenuView buildMenuView, IPlayerCameraInteractionApplier cameraInteractionApplier)
        {
            _buildMenuView = buildMenuView;
            _cameraInteractionApplier = cameraInteractionApplier;
        }

        public void OnEnter(UITransitContext context)
        {
            // メニュー中はカーソル解放・回転停止
            // Release cursor and stop rotation in menus
            _cameraInteractionApplier.SetCursorVisible(true);
            _cameraInteractionApplier.SetCameraRotatable(false);

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
            _buildMenuView.SetActive(false);
        }

        public void RestoreAfterApplicationFocus()
        {
            _cameraInteractionApplier.SetCursorVisible(true);
            _cameraInteractionApplier.SetCameraRotatable(false);
        }
    }
}
