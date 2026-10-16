using System;
using Client.PlaytestReceiver.Delivery;

namespace Client.WebUiHost.Game.Topics
{
    // 状態固有の値だけを送り、null値はWebUiJsonで省略する
    // Send only state-specific fields; WebUiJson omits null fields
    public sealed class PlaytestDeliveryDto
    {
        public readonly string Kind;
        public readonly int? PendingCount;
        public readonly int? FailedCount;
        public readonly string Reason;

        private PlaytestDeliveryDto(string kind, int? pendingCount, int? failedCount, string reason)
        {
            Kind = kind;
            PendingCount = pendingCount;
            FailedCount = failedCount;
            Reason = reason;
        }

        internal static PlaytestDeliveryDto From(PlaytestDeliveryStatus status)
        {
            return status switch
            {
                PlaytestDeliveryLocalOnly local => new PlaytestDeliveryDto("localOnly", local.PendingCount, local.FailedCount, DescribeReason(local.Reason)),
                PlaytestDeliveryPending pending => new PlaytestDeliveryDto("pending", pending.PendingCount, pending.FailedCount, null),
                PlaytestDeliveryRefused refused => new PlaytestDeliveryDto("refused", refused.PendingCount, refused.FailedCount, null),
                PlaytestDeliveryAllSent => new PlaytestDeliveryDto("allSent", null, null, null),
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown delivery status"),
            };

            #region Internal

            // 理由をwire契約の語彙へ変換する
            // Map reasons into the wire contract vocabulary
            string DescribeReason(PlaytestLocalOnlyReason reason)
            {
                return reason switch
                {
                    PlaytestLocalOnlyReason.DeveloperTokenMissing => "developerTokenMissing",
                    PlaytestLocalOnlyReason.DeveloperTokenUnreadable => "developerTokenUnreadable",
                    PlaytestLocalOnlyReason.SteamNotRunning => "steamNotRunning",
                    _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown local-only reason"),
                };
            }

            #endregion
        }

    }
}
