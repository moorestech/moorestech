using System;

namespace Client.Game.InGame.Playtest.Progress
{
    // ワールドの作成日時と累計プレイ時間。2つは独立に欠けうるので、項目ごとに値と欠損理由を組で運ぶ
    // The world's creation time and total play time; each can be missing on its own, so every item carries its value paired with its own missing reason
    public readonly struct ProgressWorldPlayTime
    {
        // 理由が null の時だけ実値。欠損時の値は読まない
        // A value is real only while its reason is null; a missing item's value is never read
        public string WorldCreatedAt { get; }
        public string WorldCreatedAtMissingReason { get; }
        public double TotalPlaySeconds { get; }
        public string TotalPlaySecondsMissingReason { get; }

        // 受け取った瞬間。終了時刻との差だけを累計へ足すので、ロードと応答待ちのぶんが二重計上されない
        // The instant it arrived; only the span to the session end is added to the total, so the load and the response wait are never counted twice
        public DateTime CapturedAtUtc { get; }

        private ProgressWorldPlayTime(string worldCreatedAt, string worldCreatedAtMissingReason, double totalPlaySeconds, string totalPlaySecondsMissingReason, DateTime capturedAtUtc)
        {
            WorldCreatedAt = worldCreatedAt;
            WorldCreatedAtMissingReason = worldCreatedAtMissingReason;
            TotalPlaySeconds = totalPlaySeconds;
            TotalPlaySecondsMissingReason = totalPlaySecondsMissingReason;
            CapturedAtUtc = capturedAtUtc;
        }

        public static ProgressWorldPlayTime Received(string worldCreatedAt, string worldCreatedAtMissingReason, double totalPlaySeconds, string totalPlaySecondsMissingReason, DateTime capturedAtUtc)
        {
            return new ProgressWorldPlayTime(worldCreatedAt, worldCreatedAtMissingReason, totalPlaySeconds, totalPlaySecondsMissingReason, capturedAtUtc);
        }

        // 応答そのものが無い時は、両項目が同じ理由でそれぞれ欠損する
        // When no response arrived at all, both items go missing, each carrying the same reason
        public static ProgressWorldPlayTime Unavailable(string reason)
        {
            return new ProgressWorldPlayTime(null, reason, 0, reason, DateTime.MinValue);
        }
    }
}
