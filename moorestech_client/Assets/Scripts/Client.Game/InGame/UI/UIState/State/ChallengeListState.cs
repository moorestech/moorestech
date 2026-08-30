using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.UIState.State.CancelInput;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class ChallengeListState : IUIState
    {
        private readonly ChallengeListView _challengeListView;
        private readonly RightShortPressInputService _rightShortPressInputService;

        public ChallengeListState(ChallengeListView challengeListView, RightShortPressInputService rightShortPressInputService)
        {
            _challengeListView = challengeListView;
            _rightShortPressInputService = rightShortPressInputService;
        }
        
        public void OnEnter(UITransitContext context)
        {
            // 他UIState滞在中は右短押しがpollされないため、復帰直後の古い押下状態を破棄する
            // Right short press isn't polled while another UIState is active, so discard any stale press state on return
            _rightShortPressInputService.ResetPressState();

            _challengeListView.SetActive(true);
            InputManager.MouseCursorVisible(true);
        }

        public UITransitContext GetNextUpdate()
        {
            var isRightShortPressed = _rightShortPressInputService.TryConsumeShortPressOutsideUi();
            //TODO InputManagerに移す
            if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.T) || isRightShortPressed) return new UITransitContext(UIStateEnum.GameScreen);
            if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);

            return null;
        }
        public void OnExit()
        {
            _challengeListView.SetActive(false);
            InputManager.MouseCursorVisible(false);
        }

        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return ChallengeListStateHints.Hints;
        }
    }

    // Tが機能停止中で入口が無いため、この画面にはヒントを置かない（ADR-0032）
    // T is disabled so this screen has no entry point; it carries no hints (ADR-0032)
    internal static class ChallengeListStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = System.Array.Empty<KeyHint>();
    }
}
