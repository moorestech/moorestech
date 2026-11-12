using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.PauseMenu
{
    /// <summary>
    /// バグ報告画面の状態・配送・書き出しの文を組む純関数。Web の BugReportForm と同じ分岐
    /// Pure functions composing the bug-report page sentences, mirroring the Web BugReportForm branches
    /// </summary>
    public static class BugReportFormText
    {
        private const string Separator = " / ";

        public static UiText DescribeStatus(BugReportCaptureStatus capture, bool sending, PlaytestReportKind draftKind)
        {
            // 押下中だけは応答待ちの手元の状態を優先する
            // Only the click's own round trip is read locally
            if (sending) return new UiText(SendingKey(draftKind));
            switch (capture.Kind)
            {
                case BugReportCaptureStatus.NoSession: return new UiText(LocalizationKeys.Ui.BugReport.NoSession);
                case BugReportCaptureStatus.Capturing: return new UiText(LocalizationKeys.Ui.BugReport.CapturePending);
                case BugReportCaptureStatus.Submitting: return new UiText(SendingKey(draftKind));
                default: return null;
            }
        }

        public static UiText DescribeMissing(BugReportCaptureStatus capture)
        {
            if (capture.Missing.Count == 0) return null;
            return new UiText(LocalizationKeys.Ui.BugReport.Missing, Params(("items", string.Join(", ", capture.Missing))));
        }

        public static UiText DescribeDelivery(PlaytestDeliveryStatus delivery)
        {
            if (delivery is PlaytestDeliveryAllSent) return new UiText(LocalizationKeys.Ui.BugReport.DeliveryAllSent);

            // 在庫の読みの 2 種で行を組み、失敗件数があれば末尾へ足す
            // Compose the line per inventory-read kind, appending the failed count when present
            var parts = new List<UiText>();
            switch (InventoryOf(delivery))
            {
                case PlaytestOutboxInventoryUnavailable unavailable:
                    parts.AddRange(DescribeUnknownCounts(delivery, unavailable));
                    break;
                case PlaytestOutboxDeliveryCounts counts:
                    parts.Add(DescribeKnownCounts(delivery, counts));
                    if (0 < counts.FailedCount) parts.Add(new UiText(LocalizationKeys.Ui.BugReport.DeliveryFailed, Params(("count", Number(counts.FailedCount)))));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(delivery), delivery, "[BugReportFormText] delivery carries no known inventory read");
            }
            return UiText.Join(Separator, parts);
        }

        // Idle は行なし。呼ぶ側が HasExportLine で先に絞る
        // Idle has no line; callers filter with HasExportLine first
        public static bool HasExportLine(PlaytestExportStatus export)
        {
            switch (export)
            {
                case PlaytestExportIdle _: return false;
                case PlaytestExportRunning _:
                case PlaytestExportFailed _:
                case PlaytestExportDone _: return true;
                default: throw new ArgumentOutOfRangeException(nameof(export), export, "[BugReportFormText] unknown export status");
            }
        }

        public static UiText DescribeExport(PlaytestExportStatus export)
        {
            switch (export)
            {
                case PlaytestExportRunning _: return new UiText(LocalizationKeys.Ui.BugReport.ExportRunning);
                case PlaytestExportFailed failed: return new UiText(LocalizationKeys.Ui.BugReport.ExportFailed, Params(("reason", failed.Reason)));
                case PlaytestExportDone done: return DescribeExportDone(done);
                default: throw new ArgumentOutOfRangeException(nameof(export), export, "[BugReportFormText] export status has no line; check HasExportLine first");
            }
        }

        // 再送は送れる経路で失敗件数があるときだけ。localOnly・refused では失敗理由が消えるので出さない
        // Resend only on a route that can send with failures; on localOnly or refused it would erase the failure reason
        public static bool ShowResend(PlaytestDeliveryStatus delivery)
        {
            if (delivery is PlaytestDeliveryLocalOnly || delivery is PlaytestDeliveryRefused) return false;
            return InventoryOf(delivery) is PlaytestOutboxDeliveryCounts counts && 0 < counts.FailedCount;
        }

        // 件数が読めないときも書き出しだけは出す。読めない箱の救出が書き出しの役目のため
        // Export stays available when counts are unreadable, since rescuing such boxes is its job
        public static bool ShowExport(PlaytestDeliveryStatus delivery)
        {
            var inventory = InventoryOf(delivery);
            if (inventory is PlaytestOutboxInventoryUnavailable) return true;
            return inventory is PlaytestOutboxDeliveryCounts counts && 0 < counts.PendingCount + counts.FailedCount;
        }

        public static LocalizationKey SendLabel(PlaytestReportKind kind)
        {
            return kind == PlaytestReportKind.Feedback ? LocalizationKeys.Ui.FeedbackReport.Send : LocalizationKeys.Ui.BugReport.Send;
        }

        public static LocalizationKey SentToast(PlaytestReportKind kind)
        {
            return kind == PlaytestReportKind.Feedback ? LocalizationKeys.Ui.FeedbackReport.Sent : LocalizationKeys.Ui.BugReport.Sent;
        }

        private static LocalizationKey SendingKey(PlaytestReportKind kind)
        {
            return kind == PlaytestReportKind.Feedback ? LocalizationKeys.Ui.FeedbackReport.Sending : LocalizationKeys.Ui.BugReport.Sending;
        }

        private static PlaytestOutboxInventoryRead InventoryOf(PlaytestDeliveryStatus delivery)
        {
            return delivery is IPlaytestDeliveryWithInventory withInventory ? withInventory.Inventory : null;
        }

        private static IEnumerable<UiText> DescribeUnknownCounts(PlaytestDeliveryStatus delivery, PlaytestOutboxInventoryUnavailable unavailable)
        {
            var inventoryLine = new UiText(LocalizationKeys.Ui.BugReport.DeliveryInventoryUnavailable, Params(("reason", unavailable.Reason)));
            switch (delivery)
            {
                case PlaytestDeliveryRefused _: return new[] { new UiText(LocalizationKeys.Ui.BugReport.DeliveryRefusedUnknown), inventoryLine };
                case PlaytestDeliveryLocalOnly localOnly: return new[] { new UiText(LocalOnlyUnknownKey(localOnly.Reason)), inventoryLine };
                default: return new[] { inventoryLine };
            }
        }

        private static UiText DescribeKnownCounts(PlaytestDeliveryStatus delivery, PlaytestOutboxDeliveryCounts counts)
        {
            var countParams = Params(("count", Number(counts.PendingCount)));
            switch (delivery)
            {
                case PlaytestDeliveryRefused _: return new UiText(LocalizationKeys.Ui.BugReport.DeliveryRefused, countParams);
                case PlaytestDeliveryLocalOnly localOnly: return new UiText(LocalOnlyKnownKey(localOnly.Reason), countParams);
                default: return new UiText(LocalizationKeys.Ui.BugReport.DeliveryPending, countParams);
            }
        }

        private static LocalizationKey LocalOnlyKnownKey(PlaytestLocalOnlyReason reason)
        {
            switch (reason)
            {
                case PlaytestLocalOnlyReason.DeveloperTokenMissing: return LocalizationKeys.Ui.BugReport.DeliveryLocalOnlyTokenMissing;
                case PlaytestLocalOnlyReason.DeveloperTokenUnreadable: return LocalizationKeys.Ui.BugReport.DeliveryLocalOnlyTokenUnreadable;
                default: return LocalizationKeys.Ui.BugReport.DeliveryLocalOnlyDistributionUnavailable;
            }
        }

        private static LocalizationKey LocalOnlyUnknownKey(PlaytestLocalOnlyReason reason)
        {
            switch (reason)
            {
                case PlaytestLocalOnlyReason.DeveloperTokenMissing: return LocalizationKeys.Ui.BugReport.DeliveryLocalOnlyTokenMissingUnknown;
                case PlaytestLocalOnlyReason.DeveloperTokenUnreadable: return LocalizationKeys.Ui.BugReport.DeliveryLocalOnlyTokenUnreadableUnknown;
                default: return LocalizationKeys.Ui.BugReport.DeliveryLocalOnlyDistributionUnavailableUnknown;
            }
        }

        private static UiText DescribeExportDone(PlaytestExportDone done)
        {
            var parts = new List<UiText> { new UiText(LocalizationKeys.Ui.BugReport.ExportDone, Params(("count", Number(done.BoxCount)), ("path", done.ZipPath))) };
            if (0 < done.Missing.Count)
            {
                var items = string.Join(", ", done.Missing.Select(missing => $"{missing.Item} ({missing.Reason})"));
                parts.Add(new UiText(LocalizationKeys.Ui.BugReport.ExportMissing, Params(("count", Number(done.Missing.Count)), ("items", items))));
            }
            if (!done.Revealed) parts.Add(new UiText(LocalizationKeys.Ui.BugReport.ExportNotRevealed));
            return UiText.Join(Separator, parts);
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static IReadOnlyList<LocalizationTextParam> Params(params (string name, string value)[] pairs)
        {
            return pairs.Select(pair => new LocalizationTextParam(pair.name, pair.value)).ToList();
        }
    }
}
