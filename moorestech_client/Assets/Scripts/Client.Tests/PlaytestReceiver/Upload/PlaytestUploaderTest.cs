using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestUploaderTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;

        [SetUp]
        public void CreateRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "playtest-upload-" + Path.GetRandomFileName());
            _directories = new PlaytestOutboxDirectories(Path.Combine(_root, "BugReports", "outbox"), Path.Combine(_root, "ProgressRecords", "outbox"));
            Directory.CreateDirectory(_directories.ReportOutbox);
            Directory.CreateDirectory(_directories.ProgressOutbox);
        }

        [TearDown]
        public void DeleteRoot()
        {
            Directory.Delete(_root, true);
        }

        [Test]
        public void READYの箱が送られUPLOADEDが付き再送されない()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{\"kind\":\"bug\"}"));
            var api = new FakeUploadApi();

            Assert.AreEqual(1, Upload(api));
            Assert.AreEqual(0, Upload(api));
            Assert.AreEqual(new[] { "manifest.json" }, api.PutPaths.ToArray());
            Assert.AreEqual(1, api.CompleteCount);
            StringAssert.Contains("\"kind\":\"report\"", api.LastSummary);
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 到達不能は数えず走行を打ち切り箱を残す()
        {
            // 残りの箱も同じ理由で失敗するので試さない。一時的な失敗で箱を諦める方向へ数えない
            // The remaining boxes would fail for the same reason, so they are not tried; a transient failure is never counted
            var first = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_110000_aaaa", ("manifest.json", "{}"));
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_bbbb", ("manifest.json", "{}"));
            var api = new FakeUploadApi { PutResult = PlaytestApiResult.TransportFailure("offline") };

            Assert.AreEqual(0, Upload(api));
            Assert.AreEqual(1, api.PutAttemptCount);
            Assert.IsFalse(File.Exists(Path.Combine(first, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(first, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 混み合いの5xxは数えずに次の箱へ進む()
        {
            var first = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_110000_aaaa", ("manifest.json", "{}"));
            var second = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_bbbb", ("manifest.json", "{}"));
            var api = new FakeUploadApi();
            api.PutResultQueue.Enqueue(PlaytestApiResult.Responded(503, "busy"));

            Assert.AreEqual(1, Upload(api));
            Assert.IsFalse(File.Exists(Path.Combine(first, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsTrue(File.Exists(Path.Combine(second, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 権利の問題が5回に達した箱はUPLOAD_FAILEDになり後続の箱を塞がない()
        {
            var stuck = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_110000_aaaa", ("manifest.json", "{}"));
            var api = new FakeUploadApi { PutResult = PlaytestApiResult.Responded(403, "revoked") };
            LogAssert.Expect(LogType.Error, new Regex(".*giving up on 20260913_110000_aaaa.*"));
            for (var attempt = 0; attempt < PlaytestUploadAttemptLog.MaxAttempts; attempt++) Upload(api);

            Assert.IsTrue(File.Exists(Path.Combine(stuck, PlaytestOutboxScanner.FailedMarker)));

            var later = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_bbbb", ("manifest.json", "{}"));
            api.PutResult = PlaytestApiResult.Responded(200, "{}");

            Assert.AreEqual(1, Upload(api));
            Assert.IsTrue(File.Exists(Path.Combine(later, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 巨大ファイルと恒久的な4xxのファイルは見送られ箱は完了する()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"), ("bad.log", "x"));
            using (var stream = new FileStream(Path.Combine(box, "video.mp4"), FileMode.Create)) stream.SetLength(PlaytestReceiverConfig.MaxFileBytes + 1);
            var api = new FakeUploadApi();
            api.PutResultQueue.Enqueue(PlaytestApiResult.Responded(400, "bad-path"));

            Assert.AreEqual(1, Upload(api));
            Assert.AreEqual(1, api.PutPaths.Count);
            StringAssert.Contains("video.mp4", api.LastSummary);
            StringAssert.Contains("too-large", api.LastSummary);
            StringAssert.Contains("http-400", api.LastSummary);
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void トークンが取れなければ何も送らず数えずに箱を残す()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeUploadApi { SessionResult = PlaytestApiResult.TransportFailure("offline") };

            Assert.AreEqual(0, Upload(api));
            Assert.AreEqual(0, api.PutAttemptCount);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 期限切れの401はトークンを取り直して1回だけやり直す()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeUploadApi();
            api.PutResultQueue.Enqueue(PlaytestApiResult.Responded(401, "expired"));
            api.PutResultQueue.Enqueue(PlaytestApiResult.Responded(200, "{}"));

            Assert.AreEqual(1, Upload(api));
            Assert.AreEqual(2, api.PutAttemptCount);
            Assert.AreEqual(2, api.SessionCallCount);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void completeが到達不能なら数えずUPLOADEDを付けない()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeUploadApi();
            api.CompleteResultQueue.Enqueue(PlaytestApiResult.TransportFailure("offline"));

            Assert.AreEqual(0, Upload(api));
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void completeが取り直し後も401なら1回と数える()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeUploadApi();
            api.CompleteResultQueue.Enqueue(PlaytestApiResult.Responded(401, "expired"));
            api.CompleteResultQueue.Enqueue(PlaytestApiResult.Responded(401, "still"));

            Assert.AreEqual(0, Upload(api));
            Assert.AreEqual(2, api.CompleteCount);
            Assert.AreEqual("1", File.ReadAllText(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)).Split('\n')[0]);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        // 受け口はR2のキーとしてUTF-8をそのまま受ける。クライアント側だけが厳しいと実ファイルが恒久的に失われる
        // The receiver takes UTF-8 verbatim as an R2 key; a stricter client rule would lose real files for good
        [Test]
        public void 日本語や空白を含む名前も見送らずに送る()
        {
            PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("ユニティ.log", "x"), ("a b.log", "y"));
            var api = new FakeUploadApi();

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEquivalent(new[] { "ユニティ.log", "a b.log" }, api.PutPaths);
            StringAssert.Contains("\"skipped\":[]", api.LastSummary);
        }

        private int Upload(FakeUploadApi api)
        {
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var uploader = new PlaytestUploader(api, session, _directories);
            return uploader.UploadPendingAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
