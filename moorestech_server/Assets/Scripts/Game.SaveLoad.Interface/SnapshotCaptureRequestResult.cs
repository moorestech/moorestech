namespace Game.SaveLoad.Interface
{
    // 即時スナップショット要求の結果。受理なら要求IDで完了と突き合わせ、拒否なら理由が入る
    // The outcome of an immediate snapshot request: an id to match against the completion, or a rejection reason
    public sealed class SnapshotCaptureRequestResult
    {
        public bool Accepted { get; }
        public long RequestId { get; }
        public string RejectedReason { get; }

        private SnapshotCaptureRequestResult(bool accepted, long requestId, string rejectedReason)
        {
            Accepted = accepted;
            RequestId = requestId;
            RejectedReason = rejectedReason;
        }

        public static SnapshotCaptureRequestResult FromAccepted(long requestId)
        {
            return new SnapshotCaptureRequestResult(true, requestId, null);
        }

        // 拒否は要求IDを持たない。要求元は完了を待たずにこの理由をそのまま表示できる
        // A rejection carries no id; the requester stops waiting and can surface this reason as-is
        public static SnapshotCaptureRequestResult FromRejected(string rejectedReason)
        {
            return new SnapshotCaptureRequestResult(false, 0, rejectedReason);
        }
    }
}
