using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.PlaytestReceiver.Upload;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestOutboxScannerTest
    {
        private string _root;

        [SetUp]
        public void CreateRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "playtest-scanner-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void DeleteRoot()
        {
            Directory.Delete(_root, true);
        }

        [Test]
        public void READYがある箱だけを古い順にkind付きで拾う()
        {
            var reports = MakeOutbox("BugReports");
            var progress = MakeOutbox("ProgressRecords");
            MakeBox(reports, "20260913_120000_bbbb", withReady: true);
            MakeBox(reports, "20260913_110000_aaaa", withReady: true);
            MakeBox(reports, "20260913_130000_cccc", withReady: false);
            MakeBox(progress, "20260913_125959_dddd", withReady: true);

            var boxes = PlaytestOutboxScanner.ScanPending(reports, progress);

            Assert.AreEqual(3, boxes.Count);
            Assert.AreEqual(new[] { "20260913_110000_aaaa", "20260913_120000_bbbb", "20260913_125959_dddd" }, boxes.Select(box => box.BundleId).ToArray());
            Assert.AreEqual("report", boxes[0].Kind);
            Assert.AreEqual("progress", boxes[2].Kind);
        }

        [Test]
        public void UPLOADED済みとUPLOAD_FAILED済みは拾わない()
        {
            var reports = MakeOutbox("BugReports");
            var progress = MakeOutbox("ProgressRecords");
            var uploaded = MakeBox(reports, "20260913_120000_bbbb", withReady: true);
            File.WriteAllText(Path.Combine(uploaded, PlaytestOutboxScanner.UploadedMarker), "");
            var failed = MakeBox(reports, "20260913_121000_cccc", withReady: true);
            File.WriteAllText(Path.Combine(failed, PlaytestOutboxScanner.FailedMarker), "");

            Assert.IsEmpty(PlaytestOutboxScanner.ScanPending(reports, progress));
        }

        [Test]
        public void マーカーは送らず中身だけを相対パスで拾う()
        {
            var reports = MakeOutbox("BugReports");
            var box = MakeBox(reports, "20260913_120000_bbbb", withReady: true);
            Directory.CreateDirectory(Path.Combine(box, "logs"));
            File.WriteAllText(Path.Combine(box, "logs", "unity.log"), "x");
            File.WriteAllText(Path.Combine(box, "manifest.json"), "{}");
            File.WriteAllText(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker), "1\noffline");

            var files = PlaytestOutboxScanner.ListPayloadFiles(box)
                .Select(file => PlaytestOutboxScanner.ToRelativePath(box, file))
                .OrderBy(path => path)
                .ToArray();

            Assert.AreEqual(new[] { "logs/unity.log", "manifest.json" }, files);
        }

        [Test]
        public void 受け口が受け取れない文字を含む相対パスは送らない()
        {
            Assert.IsTrue(PlaytestOutboxScanner.IsSendablePath("logs/unity.log"));
            Assert.IsTrue(PlaytestOutboxScanner.IsSendablePath("frames/frame_0001.jpg"));
            Assert.IsFalse(PlaytestOutboxScanner.IsSendablePath("logs/ユニティ.log"));
            Assert.IsFalse(PlaytestOutboxScanner.IsSendablePath("logs/a b.log"));
            Assert.IsFalse(PlaytestOutboxScanner.IsSendablePath("../escape.log"));
        }

        // 印の名はバグ報告の書き出し側が正本。参照できない別アセンブリなので、ここで一致を固定する
        // The bug report writer owns the marker name; this assembly cannot reference it, so the match is pinned here
        [Test]
        public void READYの印の名はバグ報告側の正本と一致する()
        {
            Assert.AreEqual(BugReportOutbox.ReadyMarkerFileName, PlaytestOutboxScanner.ReadyMarker);
        }

        private string MakeOutbox(string name)
        {
            var path = Path.Combine(_root, name, "outbox");
            Directory.CreateDirectory(path);
            return path;
        }

        private static string MakeBox(string outbox, string bundleId, bool withReady)
        {
            var box = Path.Combine(outbox, bundleId);
            Directory.CreateDirectory(box);
            if (withReady) File.WriteAllText(Path.Combine(box, PlaytestOutboxScanner.ReadyMarker), "");
            return box;
        }
    }
}
