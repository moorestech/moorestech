using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using Client.Game.InGame.UI.Challenge;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class ChallengeListState : IUIState
    {
        private readonly ChallengeListView _challengeListView;
        
        public ChallengeListState(ChallengeListView challengeListView)
        {
            _challengeListView = challengeListView;
        }
        
        public void OnEnter(UITransitContext context)
        {
            _challengeListView.SetActive(true);
            InputManager.MouseCursorVisible(true);
        }

        public UITransitContext GetNextUpdate()
        {
            //TODO InputManagerに移す
            if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.T)) return new UITransitContext(UIStateEnum.GameScreen);
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
