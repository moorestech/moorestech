using System.IO;
using Client.Game.InGame.BugReport;

namespace Client.Tests.BugReport.Bundle
{
    // テスト用の outbox 根。運搬役が読まない一時ディレクトリなので、中断で箱が残っても本番受け口へ送られない
    // Test outbox root; the uploader never reads this temp directory, so a box left by an aborted test is never sent to the production receiver
    public sealed class TestBugReportOutboxRoot : IBugReportOutboxRoot
    {
        public static readonly string Directory = Path.Combine(Path.GetTempPath(), "moorestech-tests", "bugreport-outbox");

        public string RootDirectory => Directory;
    }
}
