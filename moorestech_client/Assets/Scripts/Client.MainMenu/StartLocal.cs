using Client.MainMenu.PopUp;
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
            // 同意・前回異常終了の確認を問い合わせる。可否と拒否理由はゲートが決め、ここは表示するだけ
            // Ask the gate about consent and the previous-crash confirmation; it decides the verdict and refusal text for display here
            var verdict = PlaytestTitleGates.EvaluateStart(nameof(StartLocal), out var refusal);
            switch (verdict)
            {
                case PlaytestStartVerdict.Passed:
                    LocalGameLauncher.StartLocalGame();
                    return;
                case PlaytestStartVerdict.RefusedWithNotice:
                    messagePopup.SetText(refusal.NoticeText);
                    return;
                case PlaytestStartVerdict.RefusedWhileConfirmationVisible:
                    // 答えるべき確認が画面に出ている。理由はゲートがログへ出している
                    // The confirmation to answer is already on screen; the gate logged the reason
                    return;
                default:
                    Debug.LogError($"[StartLocal] 未知の開始判定 {verdict} のため開始しません");
                    return;
            }
        }
    }
}
