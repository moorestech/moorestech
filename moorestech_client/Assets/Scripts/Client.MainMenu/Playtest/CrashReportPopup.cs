using Client.Localization;
using Client.Starter.Playtest.TitleGates;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu.Playtest
{
    // 前回異常終了の確認。説明を添えて送る／送らないを選ばせ、書けなかったときは閉じずにその旨を出す（ADR 0061・0065）
    // The previous-crash confirmation: send with a description or decline, and on a failed write stay open and say so (ADR 0061, 0065)
    public class CrashReportPopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_InputField descriptionInput;
        [SerializeField] private TMP_Text descriptionPlaceholderText;
        [SerializeField] private Button sendButton;
        [SerializeField] private TMP_Text sendButtonText;
        [SerializeField] private Button skipButton;
        [SerializeField] private TMP_Text skipButtonText;
        [SerializeField] private TMP_Text statusText;

        private PlaytestTitleGateSequence _sequence;

        public void Initialize(PlaytestTitleGateSequence sequence)
        {
            _sequence = sequence;
            sendButton.onClick.AddListener(() => RespondAsync(true).Forget());
            skipButton.onClick.AddListener(() => RespondAsync(false).Forget());
        }

        public void SetVisible(bool visible)
        {
            if (visible)
            {
                titleText.text = Localize.Get(LocalizationKeys.Ui.Playtest.CrashGate.Title);
                bodyText.text = Localize.Get(LocalizationKeys.Ui.Playtest.CrashGate.Body);
                descriptionPlaceholderText.text = Localize.Get(LocalizationKeys.Ui.Playtest.CrashGate.Placeholder);
                sendButtonText.text = Localize.Get(LocalizationKeys.Ui.Playtest.CrashGate.Send);
                skipButtonText.text = Localize.Get(LocalizationKeys.Ui.Playtest.CrashGate.Skip);
                statusText.text = "";
            }
            gameObject.SetActive(visible);
        }

        private async UniTask RespondAsync(bool send)
        {
            // 応答中は両ボタンを閉じる。箱の書き出し（数十MBの同期コピー）の間に二重に押させない
            // Both buttons close while answering, so the box write (a synchronous copy of tens of MB) cannot be pressed twice
            SetButtonsInteractable(false);
            statusText.text = "";

            // 書き出しが例外で抜けてもボタンを戻す。戻さないと「送らない」も押せず確認から出られない
            // The buttons come back even if the write throws; otherwise "do not send" stays disabled and the confirmation can never be left
            try
            {
                var result = await _sequence.RespondCrashReportAsync(send, descriptionInput.text);
                if (result == CrashReportResponseResult.WriteFailed) statusText.text = Localize.Get(LocalizationKeys.Ui.Playtest.Gate.RespondFailed);
                if (result == CrashReportResponseResult.AlreadyResponded || result == CrashReportResponseResult.NotAsked) Debug.LogWarning($"[PlaytestTitleGates] crash confirmation pressed but the gate answered {result}");
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            sendButton.interactable = interactable;
            skipButton.interactable = interactable;
        }
    }
}
