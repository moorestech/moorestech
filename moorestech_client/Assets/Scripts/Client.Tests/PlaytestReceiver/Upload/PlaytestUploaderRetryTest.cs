using System.IO;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload;
using Client.PlaytestReceiver.Upload.Attempt;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    // 同一走行内の再試行（ADR 0064）。待ちは Immediate で消し、呼ばれた順だけを固定する
    // Retries within one run (ADR 0064); waits are removed with Immediate and only the call order is pinned
    public class PlaytestUploaderRetryTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;

        [SetUp]
        public void CreateRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "playtest-upload-retry-" + Path.GetRandomFileName());
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
        public void 一過性の503は同一走行内で再試行され送れたファイルは飛ばされる()
        {
            var box = MakeBox("20260913_120000_aaaa", "a.bin");
            var api = new FakeUploadApi();
            api.EnqueuePut("manifest.json", PlaytestApiResult.Responded(503, "{\"error\":\"1102\"}"));

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:a.bin", "put:manifest.json", "prepare", "put:manifest.json", "complete" }, api.Calls);
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 再試行の上限を超えた一過性失敗は数えずに持ち越し次の箱へ進む()
        {
            var first = MakeBox("20260913_110000_aaaa", "a.bin");
            var second = MakeBox("20260913_120000_bbbb", "b.bin");
            var api = new FakeUploadApi();
            for (var i = 0; i < 4; i++) api.EnqueuePut("a.bin", PlaytestApiResult.Responded(503, ""));

            Assert.AreEqual(1, Upload(api));
            Assert.AreEqual(1 + PlaytestUploadRetrySchedule.Immediate.Delays.Count, api.Calls.FindAll(call => call == "put:a.bin").Count);
            Assert.IsFalse(File.Exists(Path.Combine(first, PlaytestOutboxScanner.UploadedMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(first, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsTrue(File.Exists(Path.Combine(second, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void completeのincompleteはprepareからやり直し欠けたファイルだけ送り直す()
        {
            MakeBox("20260913_120000_aaaa", "a.bin");
            var api = new FakeUploadApi();
            api.EnqueueComplete(PlaytestApiResult.Responded(409, "{\"reason\":\"incomplete\",\"missing\":[{\"path\":\"a.bin\",\"expectedBytes\":3,\"actualBytes\":null}]}"));

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:a.bin", "put:manifest.json", "complete", "prepare", "put:a.bin", "complete" }, api.Calls);
        }

        [Test]
        public void 欠損一覧の読めない409は全部送り直す()
        {
            MakeBox("20260913_120000_aaaa", "a.bin");
            var api = new FakeUploadApi();
            api.EnqueueComplete(PlaytestApiResult.Responded(409, "not json"));

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:a.bin", "put:manifest.json", "complete", "prepare", "put:a.bin", "put:manifest.json", "complete" }, api.Calls);
        }

        [Test]
        public void 署名付きURLの403は数えずにprepareからやり直す()
        {
            var box = MakeBox("20260913_120000_aaaa", "a.bin");
            var api = new FakeUploadApi();
            api.EnqueuePut("a.bin", PlaytestApiResult.Responded(403, "<Error><Code>AccessDenied</Code></Error>"));

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:a.bin", "prepare", "put:a.bin", "put:manifest.json", "complete" }, api.Calls);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
        }

        [Test]
        public void 到達不能は再試行後に走行ごと止める()
        {
            var first = MakeBox("20260913_110000_aaaa", "a.bin");
            MakeBox("20260913_120000_bbbb", "b.bin");
            var api = new FakeUploadApi();
            for (var i = 0; i < 4; i++) api.EnqueuePrepare(PlaytestApiResult.TransportFailure("unreachable"));

            Assert.AreEqual(0, Upload(api));
            Assert.AreEqual(4, api.Calls.FindAll(call => call == "prepare").Count, "2箱目には進まない");
            Assert.IsFalse(File.Exists(Path.Combine(first, PlaytestOutboxScanner.AttemptsMarker)));
        }

        [Test]
        public void completeが到達不能のままなら数えずUPLOADEDを付けない()
        {
            var box = MakeBox("20260913_120000_aaaa", "a.bin");
            var api = new FakeUploadApi();
            for (var i = 0; i < 4; i++) api.EnqueueComplete(PlaytestApiResult.TransportFailure("offline"));

            Assert.AreEqual(0, Upload(api));
            Assert.AreEqual(4, api.CompleteCount);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 宣言していないパスが返ってきたら送らずにやり直す()
        {
            MakeBox("20260913_120000_aaaa", "a.bin");
            var api = new FakeUploadApi();
            api.EnqueuePrepare(PlaytestApiResult.Responded(200, "{\"outcome\":\"prepared\",\"uploads\":[{\"path\":\"x.bin\",\"url\":\"https://r2.test/x.bin\",\"bytes\":1}]}"));

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "prepare", "put:a.bin", "put:manifest.json", "complete" }, api.Calls);
        }

        private string MakeBox(string bundleId, string payloadName)
        {
            return PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, bundleId, ("manifest.json", "{}"), (payloadName, "abc"));
        }

        private int Upload(FakeUploadApi api)
        {
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var uploader = new PlaytestUploader(api, session, _directories, PlaytestUploadRetrySchedule.Immediate);
            return uploader.UploadPendingAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
