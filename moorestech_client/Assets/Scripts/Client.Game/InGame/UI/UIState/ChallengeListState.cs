using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
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
            KeyControlDescription.Instance.SetText("T: リストを閉じる");
        }

        public UITransitContext GetNextUpdate()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.T))
                return new UITransitContext(UIStateEnum.GameScreen); //TODO InputManagerに移す
            if (InputManager.UI.OpenInventory.GetKeyDown)
                return new UITransitContext(UIStateEnum.PlayerInventory);

            return new UITransitContext(UIStateEnum.Current);
        }
        public void OnExit()
        {
            _challengeListView.SetActive(false);
            InputManager.MouseCursorVisible(false);
        }
    }
}