using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Text;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BugReport
{
    /// <summary>
    /// 送れず残った報告を救う再送・書き出しのボタンと書き出し状況の行（Web の BugReportRecoveryControls）
    /// Resend and export buttons rescuing unsent reports plus the export status line (Web BugReportRecoveryControls)
    /// </summary>
    public sealed class BugReportRecoveryControlsView : VisualElement
    {
        private readonly FormButton _resendButton;
        private readonly FormButton _exportButton;
        private readonly LocalizedLabel _exportStatus;

        public BugReportRecoveryControlsView(IPauseMenuCommands commands)
        {
            AddToClassList("bug-report__recovery");

            // 再送・書き出しは filled ボタン
            // Resend and export are filled buttons
            _resendButton = CreateButton("bug-report-resend-failed", LocalizationKeys.Ui.BugReport.ResendFailed);
            _resendButton.OnClicked.Subscribe(_ => commands.ResendFailedReports());
            _exportButton = CreateButton("bug-report-export-unsent", LocalizationKeys.Ui.BugReport.ExportUnsent);
            _exportButton.OnClicked.Subscribe(_ => commands.ExportUnsentReports());
            _exportStatus = new LocalizedLabel { name = "bug-report-export-status" };
            _exportStatus.AddToClassList("bug-report__status");
            // Idle は空文で隠す。文なしで載せない
            // Idle holds empty text and hides; never attach textless
            _exportStatus.SetText(UiText.FromVerbatim(string.Empty));
            Add(_resendButton);
            Add(_exportButton);
            Add(_exportStatus);
        }

        public void Apply(PlaytestDeliveryStatus delivery, PlaytestExportStatus export)
        {
            // 書き出し中は二重に走らせないよう両方とも押せなくする
            // Both buttons are disabled while exporting so neither starts a second run
            var exporting = export is PlaytestExportRunning;
            ShowIf(_resendButton, BugReportFormText.ShowResend(delivery));
            ShowIf(_exportButton, BugReportFormText.ShowExport(delivery));
            _resendButton.SetEnabled(!exporting);
            _exportButton.SetEnabled(!exporting);

            // Idle は隠し、他は文を入れる
            // Idle hides the line; others set the sentence
            var hasLine = BugReportFormText.TryDescribeExport(export, out var line);
            ShowIf(_exportStatus, hasLine);
            if (hasLine) _exportStatus.SetText(line);
        }

        private static FormButton CreateButton(string testid, LocalizationKey label)
        {
            var button = new FormButton(testid, new UiText(label), FormButtonVariant.Filled);
            button.AddToClassList("bug-report__button");
            return button;
        }

        private static void ShowIf(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
