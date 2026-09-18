using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload;
using Client.PlaytestReceiver.Upload.Attempt;
using Newtonsoft.Json.Linq;
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
        public void 箱はprepare_全ファイルPUT_completeの順で送られUPLOADEDが置かれ再送されない()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{\"kind\":\"bug\"}"), ("a.bin", "abc"));
            var api = new FakeUploadApi();

            Assert.AreEqual(1, Upload(api));
            Assert.AreEqual(0, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json", "put:a.bin", "complete" }, api.Calls);
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));

            // ファイル一覧は受け口が照合して決めるので、補足には manifest 原文と見送りだけを載せる
            // The receiver settles the file list by verification, so the supplement carries only the raw manifest and the skips
            var supplement = JObject.Parse(api.LastCompleteBody);
            Assert.IsNull(supplement["files"]);
            Assert.AreEqual("{\"kind\":\"bug\"}", supplement.Value<string>("manifest"));
        }

        [Test]
        public void ackedの応答ならPUTせずcompleteだけ送る()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeUploadApi();
            api.EnqueuePrepare(PlaytestApiResult.Responded(200, "{\"outcome\":\"acked\"}"));

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "complete" }, api.Calls);
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 上限超と予約名は宣言から外れcompleteのskippedに載る()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"), ("ACKED", "x"));
            using (var stream = new FileStream(Path.Combine(box, "video.mp4"), FileMode.Create)) stream.SetLength(PlaytestReceiverConfig.MaxFileBytes + 1);
            var api = new FakeUploadApi();

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json", "complete" }, api.Calls);
            StringAssert.Contains("{\"path\":\"ACKED\",\"reason\":\"reserved-name\",\"bytes\":1}", api.LastCompleteBody);
            StringAssert.Contains($"{{\"path\":\"video.mp4\",\"reason\":\"too-large\",\"bytes\":{PlaytestReceiverConfig.MaxFileBytes + 1}}}", api.LastCompleteBody);
        }

        [Test]
        public void 送るものが1つも無い箱は受け口へ行かず1回と数える()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa");
            using (var stream = new FileStream(Path.Combine(box, "video.mp4"), FileMode.Create)) stream.SetLength(PlaytestReceiverConfig.MaxFileBytes + 1);
            var api = new FakeUploadApi();

            Assert.AreEqual(0, Upload(api));
            CollectionAssert.IsEmpty(api.Calls);
            Assert.AreEqual("1", File.ReadAllText(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)).Split('\n')[0]);
        }

        [Test]
        public void 受け口の403が5回に達した箱はUPLOAD_FAILEDになり後続の箱を塞がない()
        {
            var stuck = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_110000_aaaa", ("manifest.json", "{}"));
            var api = new FakeUploadApi();
            for (var i = 0; i < PlaytestUploadAttemptLog.MaxAttempts; i++) api.EnqueuePrepare(PlaytestApiResult.Responded(403, "revoked"));
            LogAssert.Expect(LogType.Error, new Regex(".*giving up on 20260913_110000_aaaa.*"));
            for (var attempt = 0; attempt < PlaytestUploadAttemptLog.MaxAttempts; attempt++) Upload(api);

            Assert.IsTrue(File.Exists(Path.Combine(stuck, PlaytestOutboxScanner.FailedMarker)));
            Assert.AreEqual(PlaytestUploadAttemptLog.MaxAttempts, api.Calls.FindAll(call => call == "prepare").Count, "権利の問題は同一走行内で再試行しない");

            var later = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_bbbb", ("manifest.json", "{}"));
            Assert.AreEqual(1, Upload(api));
            Assert.IsTrue(File.Exists(Path.Combine(later, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 恒久的な4xxは再試行せず1回と数える()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeUploadApi();
            api.EnqueuePrepare(PlaytestApiResult.Responded(400, "{\"reason\":\"bad-path\"}"));

            Assert.AreEqual(0, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare" }, api.Calls);
            Assert.AreEqual("1", File.ReadAllText(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)).Split('\n')[0]);
        }

        [Test]
        public void トークンが取れなければ何も送らず数えずに箱を残す()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeUploadApi { SessionResult = PlaytestApiResult.TransportFailure("offline") };

            Assert.AreEqual(0, Upload(api));
            CollectionAssert.IsEmpty(api.Calls);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 期限切れの401はトークンを取り直して1回だけやり直す()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeUploadApi();
            api.EnqueuePrepare(PlaytestApiResult.Responded(401, "expired"));

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "prepare", "put:manifest.json", "complete" }, api.Calls);
            Assert.AreEqual(2, api.SessionCallCount);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
        }

        [Test]
        public void completeが取り直し後も401なら1回と数える()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeUploadApi();
            api.EnqueueComplete(PlaytestApiResult.Responded(401, "expired"));
            api.EnqueueComplete(PlaytestApiResult.Responded(401, "still"));

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
            CollectionAssert.AreEqual(new[] { "prepare", "put:a b.log", "put:ユニティ.log", "complete" }, api.Calls);
            StringAssert.Contains("\"skipped\":[]", api.LastCompleteBody);
        }

        private int Upload(FakeUploadApi api)
        {
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var uploader = new PlaytestUploader(api, session, _directories, PlaytestNoWaitRetrySchedule.Create());
            return uploader.UploadPendingAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
