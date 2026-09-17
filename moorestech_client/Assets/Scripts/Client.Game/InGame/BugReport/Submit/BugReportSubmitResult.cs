namespace Client.Game.InGame.BugReport.Submit
{
    // 送信手続きの結末。成功なら書いた箱の場所、失敗なら理由コード（書き出し失敗時は箱の場所も）を持つ
    // Outcome of the send procedure; success carries the written box, failure carries a reason code (plus the box when writing failed)
    public readonly struct BugReportSubmitResult
    {
        public const string BundleWriteFailed = "bundle_write_failed";

        public string BundleDirectory { get; }
        public string FailureCode { get; }
        public bool Submitted => FailureCode == null;

        private BugReportSubmitResult(string bundleDirectory, string failureCode)
        {
            BundleDirectory = bundleDirectory;
            FailureCode = failureCode;
        }

        public static BugReportSubmitResult Succeed(string bundleDirectory)
        {
            return new BugReportSubmitResult(bundleDirectory, null);
        }

        public static BugReportSubmitResult Fail(string failureCode, string bundleDirectory)
        {
            return new BugReportSubmitResult(bundleDirectory, failureCode);
        }
    }
}
