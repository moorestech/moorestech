namespace Client.Game.InGame.BugReport.Capture
{
    // 送信中の再送を塞ぐ門
    // The gate blocks another send while a write is in progress
    public sealed class BugReportSubmitGate
    {
        private bool _inFlight;

        // 送信の書き出しが進行中か。前回確保の一時資源を消してよいかの判断に使う
        // Whether a send is still writing; used to decide if the previous capture's materials may be dropped
        public bool IsInFlight => _inFlight;

        public void Reset()
        {
            _inFlight = false;
        }

        // 送信可否の判定式。入口と配信は同じ結論を読む
        // The send entry and published state read the same permission verdict
        public string Inspect(BugReportCapturedData data, bool capturePending)
        {
            if (data == null) return BugReportSubmitTicket.NoCaptureSession;
            if (_inFlight) return BugReportSubmitTicket.SubmitInFlight;
            if (capturePending) return BugReportSubmitTicket.CapturePending;
            return null;
        }

        public BugReportSubmitTicket TryBegin(BugReportCapturedData data, bool capturePending)
        {
            var refusedCode = Inspect(data, capturePending);
            if (refusedCode != null) return Refuse(refusedCode);

            _inFlight = true;
            return BugReportSubmitTicket.Allow(data);
        }

        public void Complete()
        {
            _inFlight = false;
        }

        private static BugReportSubmitTicket Refuse(string code)
        {
            UnityEngine.Debug.LogWarning($"バグ報告を送信しません code:{code} reason:{ReasonOf(code)}");
            return BugReportSubmitTicket.Refuse(code);
        }

        private static string ReasonOf(string code)
        {
            switch (code)
            {
                case BugReportSubmitTicket.NoCaptureSession: return "確保セッションが無い";
                case BugReportSubmitTicket.SubmitInFlight: return "前の送信がまだ書き出し中";
                case BugReportSubmitTicket.CapturePending: return "記録の確保がまだ終わっていない";
                default: return "理由の文言が未定義の拒否コード";
            }
        }
    }
}
