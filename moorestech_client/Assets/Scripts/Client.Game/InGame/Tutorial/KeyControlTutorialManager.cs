using System;
using Client.Game.InGame.UI.UIState;
using Game.Challenge;
using Game.Challenge.Config.TutorialParam;
using TMPro;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial
{
    public class KeyControlTutorialManager : MonoBehaviour, ITutorialView, ITutorialViewManager
    {
        [SerializeField] private TMP_Text keyControlTutorialText;
        
        private KeyControlTutorialParam _keyControlTutorialParam;
        private UIStateControl _uiStateControl;
        
        [Inject]
        public void Construct(UIStateControl uiStateControl)
        {
            _uiStateControl = uiStateControl;
        }
        
        private void Update()
        {
            if (_keyControlTutorialParam != null)
            {
                var active = _uiStateControl.CurrentState.ToString() == _keyControlTutorialParam.UiState;
                gameObject.SetActive(active);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        
        public ITutorialView ApplyTutorial(ITutorialParam param)
        {
            _keyControlTutorialParam = (KeyControlTutorialParam)param;
            keyControlTutorialText.text = _keyControlTutorialParam.ControlText;
            return this;
        }
        
        public void CompleteTutorial()
        {
            _keyControlTutorialParam = null;
            gameObject.SetActive(false);
        }
    }
}