namespace Client.Game.InGame.BugReport.Capture
{
    // 1つの確保から送信してよい回数を1回に保つ門。送信中の再送と送信後の再送を同じ場所で塞ぐ
    // The gate that keeps one capture to one send; it blocks both re-sends during a write and re-sends after one
    public sealed class BugReportSubmitGate
    {
        private bool _inFlight;
        private bool _submitted;

        public void Reset()
        {
            _inFlight = false;
            _submitted = false;
        }

        public BugReportSubmitTicket TryBegin(BugReportCapturedData data, bool capturePending)
        {
            if (data == null) return Refuse(BugReportSubmitTicket.NoCaptureSession, "確保セッションが無い");
            if (_inFlight) return Refuse(BugReportSubmitTicket.SubmitInFlight, "前の送信がまだ書き出し中");
            if (_submitted) return Refuse(BugReportSubmitTicket.AlreadySubmitted, "この確保は既に送信済み");
            if (capturePending) return Refuse(BugReportSubmitTicket.CapturePending, "記録の確保がまだ終わっていない");

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

        private static BugReportSubmitTicket Refuse(string code, string reason)
        {
            UnityEngine.Debug.LogWarning($"バグ報告を送信しません code:{code} reason:{reason}");
            return BugReportSubmitTicket.Refuse(code);
        }
    }
}
