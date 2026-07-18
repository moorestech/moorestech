using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState;
using Mooresmaster.Model.ChallengesModule;
using TMPro;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial
{
    public class KeyControlTutorialManager : MonoBehaviour, ITutorialView, ITutorialViewManager
    {
        [SerializeField] private GameObject keyControlUIObject;
        [SerializeField] private TMP_Text keyControlTutorialText;
        private KeyControlTutorialParam _keyControlTutorialParam;
        [Inject] private UIStateControl _uiStateControl;

        private void Start()
        {
            _uiStateControl.OnStateChanged += HandleStateChanged;
            RefreshPresentation();
        }

        private void OnDestroy()
        {
            _uiStateControl.OnStateChanged -= HandleStateChanged;
        }

        public ITutorialView ApplyTutorial(ITutorialParam param)
        {
            _keyControlTutorialParam = (KeyControlTutorialParam)param;
            keyControlTutorialText.text = _keyControlTutorialParam.ControlText;
            RefreshPresentation();
            return this;
        }

        public void CompleteTutorial()
        {
            ClearPresentation();
        }

        public void ClearPresentation()
        {
            _keyControlTutorialParam = null;
            keyControlUIObject.SetActive(false);
            if (WebUiScreenGate.IsWebUiMode) KeyControlDescription.Instance.ClearOverrideText();
        }

        private void HandleStateChanged(UIStateEnum state)
        {
            RefreshPresentation();
        }

        private void RefreshPresentation()
        {
            var active = _keyControlTutorialParam != null &&
                         _uiStateControl.CurrentState.ToString() == _keyControlTutorialParam.UiState;

            // TMP表示は残しつつWebモードだけ共通key-hint sourceへ上書きする
            // Retain the TMP view while overriding the shared key-hint source only in Web mode
            keyControlUIObject.SetActive(active && !WebUiScreenGate.IsWebUiMode);
            if (!WebUiScreenGate.IsWebUiMode) return;
            if (active)
                KeyControlDescription.Instance.SetOverrideText(_keyControlTutorialParam.ControlText);
            else
                KeyControlDescription.Instance.ClearOverrideText();
        }
    }
}
