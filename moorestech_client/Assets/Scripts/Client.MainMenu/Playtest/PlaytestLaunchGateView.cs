using System;
using Client.Game.InGame.BugReport.Submit;
using Client.Localization;
using Client.MainMenu.PopUp;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Gate;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Client.PlaytestReceiver.Upload;
using Client.Starter.Playtest.TitleGates;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.MainMenu.Playtest
{
    // タイトルの合成ルート。照合結果を購読して理由を出し、通ったらタイトルのゲート（同意・前回異常終了の確認）を始めて段階をポップアップへ映す（ADR 0065）
    // The title's composition root: mirrors the launch verdict, and once it passes begins the title gates (consent, previous-crash confirmation) and reflects their step onto the popups (ADR 0065)
    public class PlaytestLaunchGateView : MonoBehaviour
    {
        [SerializeField] private ServerConnectPopup messagePopup;
        [SerializeField] private PlaytestConsentPopup consentPopup;
        [SerializeField] private CrashReportPopup crashReportPopup;

        private IPlaytestUploadRequester _uploadRequester;

        private void Start()
        {
            // MainMenuにはDIコンテナが無いので、ここを合成ルートとして受け口と走行役を組む
            // The MainMenu scene has no DI container, so this is the composition root for the receiver client and the runner
            var receiver = new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl);
            _uploadRequester = new PlaytestUploadRunner(receiver, PlaytestOutboxDirectories.FromGameSystemPaths());

            PlaytestLaunchGate.Current.Subscribe(Show).AddTo(this);
            PlaytestLaunchGate.EvaluateAsync(new PlaytestSteamTicketProvider(), receiver, DateTime.UtcNow, destroyCancellationToken).Forget();
        }

        private void Show(PlaytestGateResult result)
        {
            if (result.Status == PlaytestGateStatus.NotEvaluated) return;
            if (result.IsBlocked)
            {
                messagePopup.SetText(Localize.Get(result.ReasonKey));
                return;
            }

            // 照合しない起動（Editor・自作ビルド）は待ち文言を出していないので閉じない
            // A launch that is never checked showed no waiting message, so there is nothing to close
            if (result.Status == PlaytestGateStatus.Allowed) messagePopup.gameObject.SetActive(false);
            BeginTitleGates(result);
        }

        private void BeginTitleGates(PlaytestGateResult result)
        {
            // 初期化失敗でタイトルへ戻った再訪では始め直さない。退避と確認は起動1回に1度（ADR 0060 裁定5）
            // A revisit after a failed initialization does not restart them; salvage and confirmations happen once per boot (ADR 0060 adjudication 5)
            if (PlaytestTitleGates.Step.Value != PlaytestTitleGateStep.NotStarted)
            {
                Debug.Log($"[PlaytestTitleGates] already {PlaytestTitleGates.Step.Value}; the title gates are not restarted on this title visit");
                return;
            }

            var sequence = PlaytestTitleGates.Begin(result, _uploadRequester, destroyCancellationToken);
            consentPopup.Initialize(sequence);
            crashReportPopup.Initialize(sequence);

            // 表示は段階を映すだけ。購読はこの常時有効な合成ルートが持つ（非アクティブのポップアップにAddToしない）
            // The display only mirrors the step; this always-active root owns the subscription (never AddTo an inactive popup)
            sequence.Step.Subscribe(step =>
            {
                consentPopup.SetVisible(step == PlaytestTitleGateStep.Consent);
                crashReportPopup.SetVisible(step == PlaytestTitleGateStep.CrashReport);
            }).AddTo(this);
        }
    }
}
