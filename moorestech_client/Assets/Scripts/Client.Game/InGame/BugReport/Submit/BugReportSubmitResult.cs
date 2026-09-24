using System.Collections.Generic;
using Client.Game.InGame.BugReport;

namespace Client.Game.InGame.BugReport.Submit
{
    // 送信手続きの結末
    // - 成功: 書いた箱の場所
    // - 失敗: 理由コード（書出し失敗時は箱の場所も）
    // Outcome of the send procedure
    // - Success: the written box
    // - Failure: a reason code (plus the box when writing failed)
    public readonly struct BugReportSubmitResult
    {
        public const string BundleWriteFailed = "bundle_write_failed";

        public string BundleDirectory { get; }
        public string FailureCode { get; }
        public IReadOnlyList<MissingItem> Missing { get; }
        public bool Submitted => FailureCode == null;

        private BugReportSubmitResult(string bundleDirectory, string failureCode, IReadOnlyList<MissingItem> missing)
        {
            BundleDirectory = bundleDirectory;
            FailureCode = failureCode;
            Missing = missing;
        }

        internal static BugReportSubmitResult Succeed(string bundleDirectory, IReadOnlyList<MissingItem> missing)
        {
            return new BugReportSubmitResult(bundleDirectory, null, missing);
        }

        internal static BugReportSubmitResult Fail(string failureCode, string bundleDirectory)
        {
            return new BugReportSubmitResult(bundleDirectory, failureCode, null);
        }
    }
}
