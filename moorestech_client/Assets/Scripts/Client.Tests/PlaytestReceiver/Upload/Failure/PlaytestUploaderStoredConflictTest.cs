using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload;
using Client.PlaytestReceiver.Upload.Attempt;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.PlaytestReceiver
{
    // R2・受け口側に既にある状態との食い違い（F12・F26）。長さ違いの既存キー・読めない宣言・バケット全体の失敗・必須ファイルの恒久的な拒否
    // Mismatches with what R2 and the receiver already hold (F12, F26): an existing key of another length, an unreadable declaration, a bucket-wide failure, a required file refused for good
    public class PlaytestUploaderStoredConflictTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;

        [SetUp]
        public void CreateRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "playtest-upload-conflict-" + Path.GetRandomFileName());
            _directories = PlaytestOutboxTestBoxes.Directories(_root);
        }

        [TearDown]
        public void DeleteRoot()
        {
            Directory.Delete(_root, true);
        }

        [Test]
        public void 補助ファイルに長さ違いの既存キーがあればそのファイルだけ見送り世代を上げて送る()
        {
            var box = MakeBox("20260913_120000_aaaa");
            var api = new FakeUploadApi();
            api.EnqueuePrepare(PreparedWithConflict("a.bin", 3, 7));

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "prepare", "put:manifest.json", "complete" }, api.Calls);
            CollectionAssert.AreEqual(new[] { 1, 2 }, api.PreparedGenerations);
            StringAssert.Contains("{\"path\":\"a.bin\",\"reason\":\"stored-length-mismatch\",\"bytes\":3}", api.LastCompleteBody);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
        }

        [Test]
        public void 必須ファイルに長さ違いの既存キーがあればPUTせず1回と数える()
        {
            var box = MakeBox("20260913_120000_aaaa");
            var api = new FakeUploadApi();
            api.EnqueuePrepare(PreparedWithConflict("manifest.json", 2, 9));

            Assert.AreEqual(0, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare" }, api.Calls);
            Assert.AreEqual("1", AttemptCount(box));
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.SkippedMarker)));
        }

        // 受け口が自分の DECLARED を読めない。人が受け口側を直すまで通らないので、やり直さず数えてエラーを残す
        // The receiver cannot read its own DECLARED; nothing passes until a person repairs the receiver side, so count without retrying and leave an error
        [Test]
        public void 受け口が宣言を読めなければ再試行せず1回と数えエラーを残す()
        {
            var box = MakeBox("20260913_120000_aaaa");
            var api = new FakeUploadApi();
            api.EnqueuePrepare(PlaytestApiResult.Responded(409, "{\"reason\":\"declaration-unreadable\"}"));
            LogAssert.Expect(LogType.Error, new Regex(".*cannot read its stored declaration.*"));

            Assert.AreEqual(0, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare" }, api.Calls);
            Assert.AreEqual("1", AttemptCount(box));
        }

        // バケットの不在や鍵の誤りはファイルの問題ではない。見送りも計数もせず、表を使ってから走行を止める（設定を直せば次の走行で送れる）
        // A missing bucket or a wrong key is not the file's problem; nothing is skipped or counted, and the run stops after the schedule (fixing the setup lets the next run ship)
        [TestCase("<Error><Code>NoSuchBucket</Code></Error>", 404)]
        [TestCase("<Error><Code>InvalidAccessKeyId</Code></Error>", 403)]
        public void バケット全体の失敗は見送りも計数もせず走行を止める(string body, int statusCode)
        {
            var first = MakeBox("20260913_110000_aaaa");
            var second = MakeBox("20260913_120000_bbbb");
            var api = new FakeUploadApi();
            var attempts = 1 + PlaytestUploadRetrySchedule.Default.Delays.Count;
            for (var i = 0; i < attempts; i++) api.EnqueuePut("a.bin", PlaytestApiResult.Responded(statusCode, body));

            Assert.AreEqual(0, Upload(api));
            Assert.AreEqual(attempts, api.Calls.FindAll(call => call == "prepare").Count, "2箱目には進まない");
            Assert.IsFalse(File.Exists(Path.Combine(first, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(first, PlaytestOutboxScanner.SkippedMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(second, PlaytestOutboxScanner.UploadedMarker)));
        }

        // 必須ファイルの恒久的な403も一過性かもしれない。表を使い切るまでやり直し、それでも拒まれたら見送らずに箱を1回と数える
        // A required file's permanent-looking 403 may still be transient; it is retried until the schedule runs out and only then counts the box, never skipping the file
        [Test]
        public void 必須ファイルの恒久的な403は再試行表を使ってから1回と数える()
        {
            var box = MakeBox("20260913_120000_aaaa");
            var api = new FakeUploadApi();
            var attempts = 1 + PlaytestUploadRetrySchedule.Default.Delays.Count;
            for (var i = 0; i < attempts; i++) api.EnqueuePut("manifest.json", PlaytestApiResult.Responded(403, "<Error><Code>SignatureDoesNotMatch</Code></Error>"));

            Assert.AreEqual(0, Upload(api));
            Assert.AreEqual(attempts, api.Calls.FindAll(call => call == "put:manifest.json").Count);
            Assert.AreEqual("1", AttemptCount(box));
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.SkippedMarker)));
        }

        private static PlaytestApiResult PreparedWithConflict(string path, long expectedBytes, long actualBytes)
        {
            var body = "{\"outcome\":\"prepared\",\"uploads\":[],\"conflicts\":[{\"path\":\"" + path + "\",\"expectedBytes\":" + expectedBytes + ",\"actualBytes\":" + actualBytes + "}],\"expiresInSeconds\":3600}";
            return PlaytestApiResult.Responded(200, body);
        }

        private string MakeBox(string bundleId)
        {
            return PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, bundleId, ("manifest.json", "{}"), ("a.bin", "abc"));
        }

        private static string AttemptCount(string box)
        {
            return File.ReadAllText(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)).Split('\n')[0];
        }

        private int Upload(FakeUploadApi api)
        {
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var uploader = new PlaytestUploader(api, session, _directories, PlaytestNoWaitRetrySchedule.Create());
            return uploader.UploadPendingAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
