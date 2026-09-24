using Client.Starter.Playtest.TitleGates;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu.Playtest
{
    // 初回だけ出る参加同意の表示。了解は段階の持ち主へ渡すだけで、静的な文言はシーン側のTextMeshProLocalizeが持つ（ADR 0061・0065）
    // The first-boot consent notice; the acknowledgement is only handed to the step owner and the static wording belongs to the scene's TextMeshProLocalize (ADR 0061, 0065)
    public class PlaytestConsentPopup : MonoBehaviour
    {
        [SerializeField] private Button agreeButton;

        private PlaytestTitleGateSequence _sequence;

        public void Initialize(PlaytestTitleGateSequence sequence)
        {
            _sequence = sequence;
            agreeButton.onClick.AddListener(Acknowledge);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void Acknowledge()
        {
            var result = _sequence.AcknowledgeConsent();
            if (result != PlaytestConsentResult.Acknowledged) Debug.LogWarning($"[PlaytestTitleGates] consent button pressed but the gate answered {result}");
        }
    }
}
