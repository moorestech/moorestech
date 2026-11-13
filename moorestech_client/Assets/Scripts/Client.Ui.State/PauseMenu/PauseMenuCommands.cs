using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.PlaytestReceiver.Recovery;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Ui.State.PauseMenu
{
    /// <summary>
    /// ポーズ・バグ報告の判断を持つ型付きコマンド。表示は知らず結果だけ返す
    /// Typed commands owning the pause and bug-report decisions; they return results and know nothing of display
    /// </summary>
    public sealed class PauseMenuCommands : IPauseMenuCommands
    {
        private readonly GameSaveRequester _saveRequester;
        private readonly SaveAndQuitPresenter _saveAndQuitPresenter;
        private readonly PauseMenuStateService _pauseMenuStateService;
        private readonly BugReportSubmitter _submitter;
        private readonly PlaytestOutboxRecovery _recovery;

        public PauseMenuCommands(GameSaveRequester saveRequester, SaveAndQuitPresenter saveAndQuitPresenter, PauseMenuStateService pauseMenuStateService, BugReportSubmitter submitter, PlaytestOutboxRecovery recovery)
        {
            _saveRequester = saveRequester;
            _saveAndQuitPresenter = saveAndQuitPresenter;
            _pauseMenuStateService = pauseMenuStateService;
            _submitter = submitter;
            _recovery = recovery;
        }

        public void Save()
        {
            _saveRequester.Save();
        }

        public void SaveAndQuit()
        {
            _saveAndQuitPresenter.SaveAndQuit();
        }

        public void ShowPage(PauseMenuPage page)
        {
            _pauseMenuStateService.ShowPage(page);
        }

        // 記録と説明文を outbox へ書き、送れたらトップへ戻す（ADR 0069）
        // Writes records and the description to the outbox and returns to the top on success (ADR 0069)
        public async UniTask<UiCommandResult> SubmitBugReport(string description, PlaytestReportKind kind)
        {
            // 種別を本文より先に見る。クラッシュは前回異常終了の箱だけが作り、ポーズからは送らせない
            // Check the kind before the body; only the previous-crash box creates crash, so the pause menu must not send it
            if (!PlaytestReportKindText.IsSubmittableFromPauseMenu(kind))
            {
                Debug.LogWarning($"プレイ報告の種別がポーズから送れないため送信しません kind:{kind}");
                return UiCommandRejected.Of(UiCommandRejectReason.UnsubmittableReportKind);
            }

            var trimmed = description.Trim();
            if (trimmed.Length == 0)
            {
                Debug.LogWarning("バグ報告の説明文が空のため送信しません");
                return UiCommandRejected.Of(UiCommandRejectReason.EmptyDescription);
            }

            // 書き出し・送信記録・送信要求の手続きは通し検証と共有する1本に寄せる
            // The write, send-record and upload-request procedure lives in the one submitter shared with the smoke run
            var submitted = await _submitter.SubmitAsync(trimmed, kind);
            if (!submitted.Submitted)
            {
                return UiCommandRejected.OverridingWireCode(UiCommandRejectReasonTraits.FromSubmitFailureCode(submitted.FailureCode), submitted.FailureCode);
            }

            // 送れたらポーズは開いたままトップへ戻す。失敗時は画面を動かさず書きかけを残す
            // After a send the pause stays open and returns to the top; on failure the page stays so the draft survives
            _pauseMenuStateService.ShowPage(PauseMenuPage.Top);
            return UiCommandAccepted.Instance;
        }

        // 送信できない報告の再送。結果は配送状況の板で届く
        // Resend of undeliverable reports; the outcome arrives on the delivery board
        public void ResendFailedReports()
        {
            _recovery.RequestResendFailed();
        }

        // 未送信報告の zip 書き出し。進行は書き出し状況の板で届く
        // Zip export of unsent reports; progress arrives on the export board
        public void ExportUnsentReports()
        {
            _recovery.RequestExportUnsent();
        }
    }
}
