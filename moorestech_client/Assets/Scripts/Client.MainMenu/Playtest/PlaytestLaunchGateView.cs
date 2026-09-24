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

        // 同じ列へ二度繋がないための表示側の覚え書き。段階そのものはゲートが持つので、ここでは繋いだ相手だけを覚える
        // The view's own note so one sequence is never wired twice; the step belongs to the gates, so only the wired instance is remembered here
        private PlaytestTitleGateSequence _boundSequence;

        private void Start()
        {
            // MainMenuにはDIコンテナが無いので、ここを合成ルートとして受け口と走行役を組む
            // The MainMenu scene has no DI container, so this is the composition root for the receiver client and the runner
            var receiver = new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl);
            _uploadRequester = new PlaytestUploadRunner(receiver, PlaytestOutboxDirectories.FromGameSystemPaths());

            PlaytestLaunchGate.Current.Subscribe(Show).AddTo(this);

            // 照合はプロセス寿命で走らせる。シーン寿命で切ると結果が出ないまま無言で止まり、次の開始経路が照合中のまま塞がれる
            // The check runs for the process lifetime; cutting it with the scene would stop it silently without a verdict and leave later start paths blocked on Checking
            PlaytestLaunchGate.EvaluateAsync(new PlaytestSteamTicketProvider(), receiver, DateTime.UtcNow, Application.exitCancellationToken)
                .Forget(exception => Debug.LogError($"[PlaytestLaunchGate] 起動時照合が例外で終わりました {exception.GetType()} {exception.Message}"));
        }

        private void Show(PlaytestGateResult result)
        {
            if (result.Status == PlaytestGateStatus.NotEvaluated) return;
            if (result.IsBlocked)
            {
                // 答え待ちの確認の上に再照合の待ち文言を重ねない。重ねると答えるべき確認が隠れ、テスターは開始も応答もできなくなる（D-C1 の繋ぎ直し経路）
                // A re-check's waiting message is never stacked on a confirmation awaiting an answer; it would hide what must be answered and leave the tester unable to start or reply (the D-C1 re-attach path)
                if (result.Status == PlaytestGateStatus.Checking && _boundSequence != null && _boundSequence.IsShowingConfirmation())
                {
                    Debug.Log($"[PlaytestLaunchGate] 確認（{_boundSequence.Step.Value}）に答える前なので再照合の待ち文言は出しません");
                    return;
                }

                messagePopup.SetText(Localize.Get(result.ReasonKey));
                return;
            }

            // 照合しない起動（Editor・自作ビルド）は待ち文言を出していないので閉じない
            // A launch that is never checked showed no waiting message, so there is nothing to close
            if (result.Status == PlaytestGateStatus.Allowed) messagePopup.gameObject.SetActive(false);
            BeginTitleGates(result);

            #region Internal

            void BeginTitleGates(PlaytestGateResult verdict)
            {
                // 始動済みかの判定はゲートが1箇所で持つ。再訪でも同じ列が返るので、未応答の確認をこの画面へ繋ぎ直せる（D-C1）
                // Whether they already started is decided in one place inside the gates; a revisit gets the same sequence back and re-wires its unanswered confirmation to this screen (D-C1)
                if (!PlaytestTitleGates.TryBegin(verdict, _uploadRequester, out var sequence)) return;
                if (_boundSequence == sequence) return;

                _boundSequence = sequence;
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

            #endregion
        }
    }
}
