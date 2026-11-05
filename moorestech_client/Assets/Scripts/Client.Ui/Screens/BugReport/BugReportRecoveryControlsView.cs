using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Ui.Foundation.Text;
using Client.Ui.State.PauseMenu;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BugReport
{
    /// <summary>
    /// 送れず残った報告を救う再送・書き出しのボタンと書き出し状況の行（Web の BugReportRecoveryControls）
    /// Resend and export buttons rescuing unsent reports plus the export status line (Web BugReportRecoveryControls)
    /// </summary>
    public sealed class BugReportRecoveryControlsView : VisualElement
    {
        private readonly Button _resendButton;
        private readonly Button _exportButton;
        private readonly Label _exportStatus;

        public BugReportRecoveryControlsView(IPauseMenuCommands commands)
        {
            AddToClassList("bug-report__recovery");
            _resendButton = CreateButton("bug-report-resend-failed", LocalizationKeys.Ui.BugReport.ResendFailed);
            _resendButton.clicked += commands.ResendFailedReports;
            _exportButton = CreateButton("bug-report-export-unsent", LocalizationKeys.Ui.BugReport.ExportUnsent);
            _exportButton.clicked += commands.ExportUnsentReports;
            _exportStatus = new Label { name = "bug-report-export-status" };
            _exportStatus.AddToClassList("bug-report__status");
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

            var statusLine = BugReportFormText.DescribeExport(export);
            ShowIf(_exportStatus, statusLine != null);
            _exportStatus.text = statusLine ?? "";
        }

        private static Button CreateButton(string testid, LocalizationKey label)
        {
            var button = new Button { name = testid, focusable = false };
            button.AddToClassList("bug-report__button");
            var localizedLabel = new LocalizedLabel { pickingMode = PickingMode.Ignore };
            localizedLabel.SetKey(label);
            button.Add(localizedLabel);
            return button;
        }

        private static void ShowIf(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
