using Client.Game.InGame.BugReport.LastSession;
using NUnit.Framework;

namespace Client.Tests.BugReport.LastSession
{
    // 共有置き場（macOSのDiagnosticReports）はOSが全アプリのクラッシュを溜めるため、絞らないと他アプリの記録まで箱に入る
    // A shared root (macOS DiagnosticReports) piles every app's crash, so without filtering other apps' records land in the box
    // 実機確認2026-09-14: 絞りが無く node のクラッシュレポート98件が kind=crash の箱へ同梱された
    // Real boot 2026-09-14: with no filter, 98 node crash reports were shipped inside a kind=crash box
    public class CrashDumpLocatorTest
    {
        [Test]
        public void 共有置き場では自分のプロセス名のクラッシュレポートだけを自分の記録として扱う()
        {
            Assert.IsTrue(CrashDumpLocator.IsOwnProcessDumpName("moorestech-2026-09-14-053442.ips", "moorestech"));
            Assert.IsTrue(CrashDumpLocator.IsOwnProcessDumpName("Unity-2026-09-14-053442.ips", "moorestech"), "Editor起動のクラッシュを自分の記録として拾えていない");
        }

        [Test]
        public void 共有置き場の他アプリのクラッシュレポートは自分の記録として扱わない()
        {
            Assert.IsFalse(CrashDumpLocator.IsOwnProcessDumpName("node-2026-09-13-230934.ips", "moorestech"));
            Assert.IsFalse(CrashDumpLocator.IsOwnProcessDumpName("Google Chrome-2026-09-13-230934.ips", "moorestech"));

            // 前方一致で通すと moorestech-helper 等の別プロセスまで自分扱いになるため、プロセス名は完全一致で見る
            // A prefix match would adopt other processes such as moorestech-helper, so the process name is compared exactly
            Assert.IsFalse(CrashDumpLocator.IsOwnProcessDumpName("moorestechhelper-2026-09-13-230934.ips", "moorestech"));
        }
    }
}
