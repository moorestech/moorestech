using System.Collections.Generic;
using System.Linq;
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

        // 98件混入の真因は述語の不在ではなく置き場の宣言だった。宣言が落ちれば絞りは効かないのでここで固定する
        // The 98-file leak came from the root declaration, not the predicate: without the declaration no filter applies
        [Test]
        public void 共有置き場はDiagnosticReportsだけでCrashes配下は専用置き場として宣言される()
        {
            var roots = CrashDumpLocator.CandidateDumpRoots();
            var shared = roots.Where(root => root.SharedWithOtherApps).ToList();

            Assert.AreEqual(1, shared.Count, "共有と宣言された置き場の数が想定と違う");
            StringAssert.Contains("DiagnosticReports", shared[0].Path, "macOSの共有置き場が共有と宣言されていない");
            Assert.IsTrue(roots.Where(root => root.Path.EndsWith(CrashDumpLocator.CrashesFolderName)).All(root => !root.SharedWithOtherApps), "<product>配下の専用置き場が共有扱いになっている");
        }

        // 選別が置き場の宣言を実際に見ていること。見なくなれば共有置き場の他アプリ分がそのまま通る
        // That the selection really consults the declaration; if it stops, a shared root's other-app files pass straight through
        [Test]
        public void 選別は共有置き場の他アプリ分だけを落とし件数と除外元を残す()
        {
            var shared = new CrashDumpRoot { Path = "/shared", SharedWithOtherApps = true };
            var dedicated = new CrashDumpRoot { Path = "/dedicated" };
            var candidates = new List<CrashDumpCandidate>
            {
                Candidate(shared, "node-2026-09-13-230934.ips"),
                Candidate(shared, "Google Chrome-2026-09-13-230934.ips"),
                Candidate(shared, "moorestech-2026-09-14-053442.ips"),
                Candidate(dedicated, "crash.dmp"),
                Candidate(shared, "notes.txt"),
            };

            var scan = CrashDumpLocator.SelectDumpFiles(candidates, "moorestech");

            CollectionAssert.AreEquivalent(new[] { "/shared/moorestech-2026-09-14-053442.ips", "/dedicated/crash.dmp" }, scan.Files);
            Assert.AreEqual(2, scan.ExcludedAsOtherApps, "他アプリとして落とした件数が残っていない");
            CollectionAssert.AreEqual(new[] { "/shared" }, scan.ExcludedRoots, "落とした置き場が残っていない");
        }

        // 専用置き場は中身が必ず自分のものなので、プロセス名がどうであれ落としてはいけない
        // A dedicated root only ever holds our own files, so nothing there may be dropped whatever the process name says
        [Test]
        public void 専用置き場のダンプはプロセス名に関わらず残る()
        {
            var dedicated = new CrashDumpRoot { Path = "/dedicated" };
            var candidates = new List<CrashDumpCandidate> { Candidate(dedicated, "node-2026-09-13-230934.ips") };

            var scan = CrashDumpLocator.SelectDumpFiles(candidates, "moorestech");

            CollectionAssert.AreEqual(new[] { "/dedicated/node-2026-09-13-230934.ips" }, scan.Files);
            Assert.AreEqual(0, scan.ExcludedAsOtherApps);
        }

        private static CrashDumpCandidate Candidate(CrashDumpRoot root, string fileName)
        {
            return new CrashDumpCandidate { Root = root, FileName = fileName, FullPath = $"{root.Path}/{fileName}" };
        }
    }
}
