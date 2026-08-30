using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Game.InGame.UI.UIState.State.CancelInput;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class BuildMenuState : IUIState, IApplicationFocusRestorer
    {
        private readonly IBuildMenuView _buildMenuView;
        private readonly UiStateCameraPolicyService _cameraPolicyService;
        private readonly RightShortPressInputService _rightShortPressInputService;

        public BuildMenuState(IBuildMenuView buildMenuView, UiStateCameraPolicyService cameraPolicyService, RightShortPressInputService rightShortPressInputService)
        {
            _buildMenuView = buildMenuView;
            _cameraPolicyService = cameraPolicyService;
            _rightShortPressInputService = rightShortPressInputService;
        }

        public void OnEnter(UITransitContext context)
        {
            // 他UIState滞在中は右短押しがpollされないため、復帰直後の古い押下状態を破棄する
            // Right short press isn't polled while another UIState is active, so discard any stale press state on return
            _rightShortPressInputService.ResetPressState();

            // メニュー中はカーソル解放・回転停止
            // Release cursor and stop rotation in menus
            _cameraPolicyService.EnterMenu();

            _buildMenuView.SetActive(true);
        }

        public UITransitContext GetNextUpdate()
        {
            // パネル外の右短押し状態を毎フレーム取得（ManualUpdateが走る前に）
            // Evaluate right short press state every frame before early returns (ManualUpdate runs internally)
            var isRightShortPressed = _rightShortPressInputService.TryConsumeShortPressOutsideUi();

            if (_buildMenuView.TryConsumeSelectedEntry(out var entry))
                return new UITransitContext(UIStateEnum.PlaceBlock, UITransitContextContainer.Create(new PlacementSelection(entry.Target, PlacementOrigin.NonHotbar)));

            if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.B) || isRightShortPressed) return new UITransitContext(UIStateEnum.GameScreen, null);
            if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory, null);

            return null;
        }

        public void OnExit()
        {
            _buildMenuView.SetActive(false);
        }

        public void RestoreAfterApplicationFocus()
        {
            _cameraPolicyService.RestoreAfterApplicationFocus();
        }

        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return BuildMenuStateHints.Hints;
        }
    }

    internal static class BuildMenuStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.B, LocalizationKeys.Ui.KeyHint.Text.Close),
        };
    }
}
