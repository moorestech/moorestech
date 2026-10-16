namespace Client.PlaytestReceiver.Upload
{
    public enum PlaytestUploadRunEnd { Finished, SessionRefused }
    public sealed class PlaytestUploadRunSummary
    {
        public readonly PlaytestUploadRunEnd End;
        public readonly int Sent;
        public PlaytestUploadRunSummary(PlaytestUploadRunEnd end, int sent) { End = end; Sent = sent; }
    }
}
