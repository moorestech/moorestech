namespace Client.Game.InGame.BugReport.Recording
{
    // 録画が使えるかと、使えないときの理由を1つの値で表す。「使えないのに理由が空」を構築不能にする
    // One value carrying whether recording works and, when it does not, why; "unavailable with an empty reason" is unconstructable
    public readonly struct RecordingAvailability
    {
        // 起動後にffmpegが死んだ場合など、latchした理由が無いまま止まっているときに使う既定の理由
        // Default reason for a stop with no latched reason, e.g. ffmpeg dying after a successful start
        public const string StoppedWithoutReason = "録画プロセスが停止しています（ログ参照）";

        public bool IsAvailable { get; }
        public string Reason { get; }

        private RecordingAvailability(bool isAvailable, string reason)
        {
            IsAvailable = isAvailable;
            Reason = reason;
        }

        public static RecordingAvailability Available()
        {
            return new RecordingAvailability(true, "");
        }

        public static RecordingAvailability Unavailable(string reason)
        {
            return new RecordingAvailability(false, string.IsNullOrEmpty(reason) ? StoppedWithoutReason : reason);
        }
    }
}
