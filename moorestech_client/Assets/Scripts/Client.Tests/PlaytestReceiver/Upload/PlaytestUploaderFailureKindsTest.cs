using System.IO;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    // 失敗の種別ごとの扱い（D2〜D4・D7）。契約違反・宣言の衝突・R2の412/403/4xx・手元の読み取り失敗
    // How each failure kind is handled (D2-D4, D7): contract breaches, declaration conflicts, R2's 412/403/4xx and local read failures
    public class PlaytestUploaderFailureKindsTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;

        [SetUp]
        public void CreateRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "playtest-upload-kinds-" + Path.GetRandomFileName());
            _directories = PlaytestOutboxTestBoxes.Directories(_root);
        }

        [TearDown]
        public void DeleteRoot()
        {
            Directory.Delete(_root, true);
        }

        [Test]
        public void prepareの2xx契約違反は数えずに持ち越し後続の箱へ進む()
        {
            var first = MakeBox("20260913_110000_aaaa");
            var second = MakeBox("20260913_120000_bbbb");
            var api = new FakeUploadApi();
            for (var i = 0; i < 4; i++) api.EnqueuePrepare(PlaytestApiResult.Responded(200, "<html>captive portal</html>"));

            Assert.AreEqual(1, Upload(api));
            Assert.IsFalse(File.Exists(Path.Combine(first, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(first, PlaytestOutboxScanner.UploadedMarker)));
            Assert.IsTrue(File.Exists(Path.Combine(second, PlaytestOutboxScanner.UploadedMarker)));
        }

        [TestCase("{}")]
        [TestCase("{\"ready\":false}")]
        [TestCase("{\"ready\":\"true\"}")]
        [TestCase("<html></html>")]
        public void completeがready_trueを返さなければUPLOADEDを付けない(string body)
        {
            var box = MakeBox("20260913_120000_aaaa");
            var api = new FakeUploadApi();
            for (var i = 0; i < 4; i++) api.EnqueueComplete(PlaytestApiResult.Responded(200, body));

            Assert.AreEqual(0, Upload(api));
            Assert.AreEqual(4, api.CompleteCount);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
        }

        [Test]
        public void prepareのdeclaration_conflictは再試行せず1回と数える()
        {
            var box = MakeBox("20260913_120000_aaaa");
            var api = new FakeUploadApi();
            api.EnqueuePrepare(PlaytestApiResult.Responded(409, "{\"reason\":\"declaration-conflict\"}"));

            Assert.AreEqual(0, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare" }, api.Calls);
            Assert.AreEqual("1", AttemptCount(box));
        }

        [Test]
        public void 署名付きPUTの412は送信済みとして進みやり直さない()
        {
            var box = MakeBox("20260913_120000_aaaa");
            var api = new FakeUploadApi();
            api.EnqueuePut("a.bin", PlaytestApiResult.Responded(412, "<Error><Code>PreconditionFailed</Code></Error>"));

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json", "put:a.bin", "complete" }, api.Calls);
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        // 412で送信済みとしたキーを complete が欠けと数えたら、R2に長さ違いの別物がある。やり直さず1回と数える
        // When complete counts missing a key taken as sent on a 412, R2 holds a different length there; count once without retrying
        [Test]
        public void 送信済みとした412のキーがcompleteで欠ければ再試行せず1回と数える()
        {
            var box = MakeBox("20260913_120000_aaaa");
            var api = new FakeUploadApi();
            api.EnqueuePut("a.bin", PlaytestApiResult.Responded(412, "<Error><Code>PreconditionFailed</Code></Error>"));
            api.EnqueueComplete(PlaytestApiResult.Responded(409, "{\"reason\":\"incomplete\",\"missing\":[{\"path\":\"a.bin\",\"expectedBytes\":3,\"actualBytes\":7}]}"));

            Assert.AreEqual(0, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json", "put:a.bin", "complete" }, api.Calls);
            Assert.AreEqual("1", AttemptCount(box));
        }

        // 期限切れ（S3の AccessDenied「Request has expired」とそれを名乗るCode）とXMLで読めない403だけが一過性。署名不一致等は補助ファイルならそのファイルだけ見送る
        // Only an expiry (S3's AccessDenied "Request has expired" and codes naming one) and a non-XML 403 are transient; a signature mismatch and the like skip just that file when it is supporting
        [TestCase("<Error><Code>AccessDenied</Code><Message>Request has expired</Message></Error>", true)]
        [TestCase("<Error><Code>RequestExpired</Code><Message>x</Message></Error>", true)]
        [TestCase("<Error><Code>ExpiredRequest</Code></Error>", true)]
        [TestCase("forbidden by a proxy", true)]
        [TestCase("<Error><Code>SignatureDoesNotMatch</Code><Message>The request signature we calculated does not match</Message></Error>", false)]
        [TestCase("<Error><Code>AccessDenied</Code><Message>Access Denied</Message></Error>", false)]
        public void 署名付きPUTの403はR2のCodeで一過性と恒久を分ける(string body, bool transient)
        {
            var box = MakeBox("20260913_120000_aaaa");
            var api = new FakeUploadApi();
            api.EnqueuePut("a.bin", PlaytestApiResult.Responded(403, body));

            Assert.AreEqual(1, Upload(api));
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
            if (transient) CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json", "put:a.bin", "prepare", "put:a.bin", "complete" }, api.Calls);
            else StringAssert.Contains("{\"path\":\"a.bin\",\"reason\":\"http-403\",\"bytes\":3}", api.LastCompleteBody);
        }

        // 消えた・長さが変わったファイルは戻らない。補助ならそのファイルだけ見送り、必須なら箱を1回と数える
        // A vanished or resized file never comes back; a supporting one alone is skipped, a required one counts the box once
        [Test]
        public void 手元で変わったファイルは補助なら見送り必須なら再試行せず1回と数える()
        {
            var box = MakeBox("20260913_110000_aaaa");
            var api = new FakeUploadApi();
            api.EnqueuePut("a.bin", PlaytestApiResult.LocalFileChanged("a.bin: the file ended before its declared 3 bytes"));

            Assert.AreEqual(1, Upload(api));
            StringAssert.Contains("{\"path\":\"a.bin\",\"reason\":\"local-file-changed\",\"bytes\":3}", api.LastCompleteBody);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));

            var required = MakeBox("20260913_120000_bbbb");
            api.Calls.Clear();
            api.EnqueuePut("manifest.json", PlaytestApiResult.LocalFileChanged("manifest.json does not exist"));
            Assert.AreEqual(0, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json" }, api.Calls);
            Assert.AreEqual("1", AttemptCount(required));
        }

        // ロック等の一時的な読み取り失敗は数えずに再試行表でやり直す
        // A temporary read failure such as a lock is retried per the schedule without counting
        [Test]
        public void 手元のファイルが一時的に読めなければ数えずに再試行する()
        {
            var box = MakeBox("20260913_120000_aaaa");
            var api = new FakeUploadApi();
            api.EnqueuePut("a.bin", PlaytestApiResult.LocalFileUnavailable("a.bin: locked"));

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json", "put:a.bin", "prepare", "put:a.bin", "complete" }, api.Calls);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
        }

        [Test]
        public void prepareがURLを返さなかった宣言ファイルはPUTせずcompleteの照合に任せる()
        {
            MakeBox("20260913_120000_aaaa");
            var api = new FakeUploadApi();
            api.EnqueuePrepare(PlaytestApiResult.Responded(200, "{\"outcome\":\"prepared\",\"uploads\":[{\"path\":\"a.bin\",\"url\":\"https://r2.test/a.bin\",\"bytes\":3}],\"conflicts\":[],\"expiresInSeconds\":3600}"));

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:a.bin", "complete" }, api.Calls);
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
