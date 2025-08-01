using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class ChallengeListState : IUIState
    {
        private readonly ChallengeListUI _challengeListUI;
        
        public ChallengeListState(ChallengeListUI challengeListUI)
        {
            _challengeListUI = challengeListUI;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _challengeListUI.UpdateUnlockState();
            _challengeListUI.SetActive(true);
            InputManager.MouseCursorVisible(true);
            KeyControlDescription.Instance.SetText("T: リストを閉じる");
        }
        public UIStateEnum GetNextUpdate()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.T)) return UIStateEnum.GameScreen; //TODO InputManagerに移す
            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            
            return UIStateEnum.Current;
        }
        public void OnExit()
        {
            _challengeListUI.SetActive(false);
            InputManager.MouseCursorVisible(false);
        }
    }
}