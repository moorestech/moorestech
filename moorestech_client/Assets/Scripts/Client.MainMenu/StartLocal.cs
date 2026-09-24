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
            // 照合と同意・前回異常終了の確認は1回の問い合わせで通す。可否も拒否理由の文言もゲートが決め、ここは表示するだけ（ADR 0065）
            // The launch check and the consent / previous-crash confirmation pass in one call; the gate decides both the verdict and the refusal text, and this only displays it (ADR 0065)
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
