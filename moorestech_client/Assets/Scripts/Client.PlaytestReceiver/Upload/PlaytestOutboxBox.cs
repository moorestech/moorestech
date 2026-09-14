namespace Client.PlaytestReceiver.Upload
{
    // 送る単位。kindは受け口のパス（report|progress）と同じ語をそのまま持つ
    // One shippable unit; Kind carries the receiver's own word (report|progress) verbatim
    public sealed class PlaytestOutboxBox
    {
        public string Directory;
        public string BundleId;
        public string Kind;
    }
}
