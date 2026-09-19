using Client.MainMenu.PopUp;
using Client.PlaytestReceiver.Gate;
using Client.Starter;
using Client.Starter.Playtest.TitleGates;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu
{
    public class StartLocal : MonoBehaviour
    {
        [SerializeField] private Button startLocalButton;
        [SerializeField] private ServerConnectPopup messagePopup;

        private void Start()
        {
            startLocalButton.onClick.AddListener(StartLocalGame);
        }

        private void StartLocalGame()
        {
            // 開始可否と拒否理由の文言はゲートが決める。ここは表示するだけ
            // The gate decides whether to start and resolves the refusal text; this only displays it
            if (!PlaytestLaunchGate.TryPassStart(nameof(StartLocal), out var denyReasonText))
            {
                messagePopup.SetText(denyReasonText);
                return;
            }

            // 同意と前回異常終了の確認に答えるまで開始しない。拒否理由はゲートがログへ出す（ADR 0065）
            // Nothing starts until the consent and previous-crash confirmation are answered; the gate logs the refusal (ADR 0065)
            if (!PlaytestTitleGates.TryPassStart(nameof(StartLocal))) return;

            LocalGameLauncher.StartLocalGame();
        }
    }
}
