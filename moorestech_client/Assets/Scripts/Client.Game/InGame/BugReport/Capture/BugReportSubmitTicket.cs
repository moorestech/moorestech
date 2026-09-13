namespace Client.Game.InGame.BugReport.Capture
{
    // 送信してよいかの判定結果。許可なら記録一式、拒否なら理由コードのどちらか一方だけを持つ
    // Outcome of the send guard; it carries either the records (allowed) or a refusal code, never both
    public readonly struct BugReportSubmitTicket
    {
        public const string NoCaptureSession = "no_capture_session";
        public const string CapturePending = "capture_pending";
        public const string AlreadySubmitted = "already_submitted";
        public const string SubmitInFlight = "submit_in_flight";

        public BugReportCapturedData Data { get; }
        public string RefusedCode { get; }
        public bool Allowed => RefusedCode == null;

        private BugReportSubmitTicket(BugReportCapturedData data, string refusedCode)
        {
            Data = data;
            RefusedCode = refusedCode;
        }

        public static BugReportSubmitTicket Allow(BugReportCapturedData data)
        {
            return new BugReportSubmitTicket(data, null);
        }

        public static BugReportSubmitTicket Refuse(string refusedCode)
        {
            return new BugReportSubmitTicket(null, refusedCode);
        }
    }
}
