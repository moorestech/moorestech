namespace Client.Game.InGame.BugReport.Capture
{
    // 1つの確保から送信してよい回数を1回に保つ門。送信中の再送と送信後の再送を同じ場所で塞ぐ
    // The gate that keeps one capture to one send; it blocks both re-sends during a write and re-sends after one
    public sealed class BugReportSubmitGate
    {
        private bool _inFlight;
        private bool _submitted;

        // 送信の書き出しが進行中か。前回確保の一時資源を消してよいかの判断に使う
        // Whether a send is still writing; used to decide if the previous capture's materials may be dropped
        public bool IsInFlight => _inFlight;

        public void Reset()
        {
            _inFlight = false;
            _submitted = false;
        }

        // 送信可否の判定式。送信の入口も外向きの配信も同じ結論を読むため、条件はここ1箇所にしか無い
        // The one send-permission rule; both the send entry point and the published state read this verdict, so the conditions live here only
        public string Inspect(BugReportCapturedData data, bool capturePending)
        {
            if (data == null) return BugReportSubmitTicket.NoCaptureSession;
            if (_inFlight) return BugReportSubmitTicket.SubmitInFlight;
            if (_submitted) return BugReportSubmitTicket.AlreadySubmitted;
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

        // 書き出せなかった送信は送信済みにしない。残った資料で送り直せる道を閉じないため
        // A write that never completed does not count as sent, so retrying with whatever survived stays possible
        public void Complete(bool ready)
        {
            _inFlight = false;
            _submitted = ready;
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
                case BugReportSubmitTicket.AlreadySubmitted: return "この確保は既に送信済み";
                case BugReportSubmitTicket.CapturePending: return "記録の確保がまだ終わっていない";
                default: return "理由の文言が未定義の拒否コード";
            }
        }
    }
}
