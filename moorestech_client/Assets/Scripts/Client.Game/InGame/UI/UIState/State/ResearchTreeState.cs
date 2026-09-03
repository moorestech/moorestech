using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using Client.Game.InGame.UI.Inventory.Block.Research;
using Client.Game.InGame.UI.UIState.State.CancelInput;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    /// <summary>
    /// 研究ツリーUIを制御するステート
    /// UI state that controls the research tree view
    /// </summary>
    public class ResearchTreeState : IUIState
    {
        private readonly ResearchTreeViewManager _researchTreeViewManager;
        private readonly RightShortPressInputService _rightShortPressInputService;

        public ResearchTreeState(ResearchTreeViewManager researchTreeViewManager, RightShortPressInputService rightShortPressInputService)
        {
            _researchTreeViewManager = researchTreeViewManager;
            _rightShortPressInputService = rightShortPressInputService;
        }

        public void OnEnter(UITransitContext context)
        {
            // 他UIState滞在中は右短押しがpollされないため、復帰直後の古い押下状態を破棄する
            // Right short press isn't polled while another UIState is active, so discard any stale press state on return
            _rightShortPressInputService.ResetPressState();

            // リサーチUIの表示とカーソル制御
            // Show research UI and update cursor
            _researchTreeViewManager.SetActive(true);
            InputManager.MouseCursorVisible(true);
        }

        public UITransitContext GetNextUpdate()
        {
            var isRightShortPressed = _rightShortPressInputService.TryConsumeShortPressOutsideUi();

            // Tabでインベントリへ、ESC/R/パネル外の右短押しでゲーム画面へ戻る
            // Go to inventory with Tab, or back to game screen with ESC, R or a right short press outside the panel
            // TODO InputManagerに移す
            if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);
            if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.R) || isRightShortPressed) return new UITransitContext(UIStateEnum.GameScreen);

            return null;
        }

        public void OnExit()
        {
            // リサーチUIを閉じてカーソルを隠す
            // Hide research UI and the cursor
            _researchTreeViewManager.SetActive(false);
            InputManager.MouseCursorVisible(false);
        }

        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return ResearchTreeStateHints.Hints;
        }
    }

    internal static class ResearchTreeStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.R, LocalizationKeys.Ui.KeyHint.Text.Close),
        };
    }
}
