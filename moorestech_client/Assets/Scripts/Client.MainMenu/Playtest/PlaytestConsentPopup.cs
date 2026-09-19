using Client.Localization;
using Client.Starter.Playtest.TitleGates;
using Mooresmaster.Localization.Generated;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu.Playtest
{
    // 初回だけ出る参加同意の表示。文言は既存キーのまま、了解は段階の持ち主へ渡すだけ（ADR 0061・0065）
    // The first-boot consent notice; the wording keeps the existing keys and the acknowledgement is only handed to the step owner (ADR 0061, 0065)
    public class PlaytestConsentPopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button agreeButton;
        [SerializeField] private TMP_Text agreeButtonText;

        private PlaytestTitleGateSequence _sequence;

        public void Initialize(PlaytestTitleGateSequence sequence)
        {
            _sequence = sequence;
            agreeButton.onClick.AddListener(Acknowledge);
        }

        // 表示のたびに文言を引き直す。タイトルの言語設定を変えた後に出ても現在の言語で読める
        // The texts are resolved on every show, so it reads in the current language even after the title's language setting changed
        public void SetVisible(bool visible)
        {
            if (visible)
            {
                titleText.text = Localize.Get(LocalizationKeys.Ui.Playtest.Consent.Title);
                bodyText.text = Localize.Get(LocalizationKeys.Ui.Playtest.Consent.Body);
                agreeButtonText.text = Localize.Get(LocalizationKeys.Ui.Playtest.Consent.Agree);
            }
            gameObject.SetActive(visible);
        }

        private void Acknowledge()
        {
            var result = _sequence.AcknowledgeConsent();
            if (result != PlaytestConsentResult.Acknowledged) Debug.LogWarning($"[PlaytestTitleGates] consent button pressed but the gate answered {result}");
        }
    }
}
