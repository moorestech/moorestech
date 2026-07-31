using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Mooresmaster.Model.ChallengesModule;
using TMPro;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial
{
    public class KeyControlTutorialManager : MonoBehaviour, ITutorialView, ITutorialViewManager
    {
        [SerializeField] private GameObject keyControlUIObject;
        [SerializeField] private TMP_Text keyControlTutorialText;
        private TutorialsElement _currentTutorial;
        private KeyControlTutorialParam _keyControlTutorialParam;
        [Inject] private UIStateControl _uiStateControl;

        private void Start()
        {
            _uiStateControl.OnStateChanged += HandleStateChanged;

            // 言語切替時に表示中の文言を再解決する
            // Re-resolve the visible text when the language changes
            Localize.OnLanguageChanged.Subscribe(_ => RefreshPresentation()).AddTo(this);
            RefreshPresentation();
        }

        private void OnDestroy()
        {
            // 初期シーン遷移中はDI注入前に破棄され得るためnull許容（ライフサイクル境界）
            // May be destroyed before DI injection during the initial scene switch, so tolerate null (lifecycle boundary)
            if (_uiStateControl != null) _uiStateControl.OnStateChanged -= HandleStateChanged;
        }

        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            _currentTutorial = tutorial;
            _keyControlTutorialParam = (KeyControlTutorialParam)tutorial.TutorialParam;
            RefreshPresentation();
            return this;
        }

        public void CompleteTutorial()
        {
            ClearPresentation();
        }

        public void ClearPresentation()
        {
            _currentTutorial = null;
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

            // 表示文言はtutorialGuidから都度解決する
            // Resolve the display text from the tutorial GUID on each refresh
            var controlText = _currentTutorial == null
                ? ""
                : Localize.GetContent(ContentLocalizationKeys.ChallengeTutorialText(_currentTutorial.TutorialGuid));
            keyControlTutorialText.text = controlText;

            // TMP表示は残しつつWebモードだけ共通key-hint sourceへ上書きする
            // Retain the TMP view while overriding the shared key-hint source only in Web mode
            keyControlUIObject.SetActive(active && !WebUiScreenGate.IsWebUiMode);
            if (!WebUiScreenGate.IsWebUiMode) return;
            if (active)
                KeyControlDescription.Instance.SetOverrideText(controlText);
            else
                KeyControlDescription.Instance.ClearOverrideText();
        }
    }
}
