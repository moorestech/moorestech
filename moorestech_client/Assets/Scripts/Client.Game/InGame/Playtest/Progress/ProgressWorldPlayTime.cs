using System;

namespace Client.Game.InGame.Playtest.Progress
{
    // ワールドの作成日時と累計プレイ時間。取れたか取れなかったかを同じ型で運び、取れなかった側は理由を持つ
    // The world's creation time and total play time; one type carries both outcomes and the unavailable one carries its reason
    public readonly struct ProgressWorldPlayTime
    {
        public string WorldCreatedAt { get; }
        public double TotalPlaySeconds { get; }

        // 受け取った瞬間。終了時刻との差だけを累計へ足すので、ロードと応答待ちのぶんが二重計上されない
        // The instant it arrived; only the span to the session end is added to the total, so the load and the response wait are never counted twice
        public DateTime CapturedAtUtc { get; }

        public string MissingReason { get; }

        private ProgressWorldPlayTime(string worldCreatedAt, double totalPlaySeconds, DateTime capturedAtUtc, string missingReason)
        {
            WorldCreatedAt = worldCreatedAt;
            TotalPlaySeconds = totalPlaySeconds;
            CapturedAtUtc = capturedAtUtc;
            MissingReason = missingReason;
        }

        public static ProgressWorldPlayTime Received(string worldCreatedAt, double totalPlaySeconds, DateTime capturedAtUtc)
        {
            return new ProgressWorldPlayTime(worldCreatedAt, totalPlaySeconds, capturedAtUtc, null);
        }

        public static ProgressWorldPlayTime Unavailable(string reason)
        {
            return new ProgressWorldPlayTime("", 0, DateTime.MinValue, reason);
        }
    }
}
