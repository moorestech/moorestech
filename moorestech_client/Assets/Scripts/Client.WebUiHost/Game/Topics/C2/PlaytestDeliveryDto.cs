using System;
using Client.PlaytestReceiver.Delivery;

namespace Client.WebUiHost.Game.Topics
{
    // 状態固有の値だけを送り、null値はWebUiJsonで省略する
    // Send only state-specific fields; WebUiJson omits null fields
    public sealed class PlaytestDeliveryDto
    {
        public string Kind;
        public int? PendingCount;
        public string Reason;

        internal static PlaytestDeliveryDto From(PlaytestDeliveryStatus status)
        {
            return status switch
            {
                PlaytestDeliveryLocalOnly local => new PlaytestDeliveryDto { Kind = "localOnly", PendingCount = local.PendingCount, Reason = DescribeReason(local.Reason) },
                PlaytestDeliveryPending pending => new PlaytestDeliveryDto { Kind = "pending", PendingCount = pending.PendingCount },
                PlaytestDeliveryRefused refused => new PlaytestDeliveryDto { Kind = "refused", PendingCount = refused.PendingCount },
                PlaytestDeliveryAllSent => new PlaytestDeliveryDto { Kind = "allSent" },
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown delivery status"),
            };
        }

        // 理由をwire契約の語彙へ変換する
        // Map reasons into the wire contract vocabulary
        private static string DescribeReason(PlaytestLocalOnlyReason reason)
        {
            return reason switch
            {
                PlaytestLocalOnlyReason.DeveloperTokenMissing => "developerTokenMissing",
                PlaytestLocalOnlyReason.DeveloperTokenUnreadable => "developerTokenUnreadable",
                PlaytestLocalOnlyReason.SteamNotRunning => "steamNotRunning",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown local-only reason"),
            };
        }
    }
}
