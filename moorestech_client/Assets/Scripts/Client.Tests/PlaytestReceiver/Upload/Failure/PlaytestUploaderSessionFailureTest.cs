using System.IO;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload;
using Client.PlaytestReceiver.Upload.Attempt;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    // トークンが取れなかった理由ごとの扱いと、箱のファイルI/O失敗の扱い（F09・F13）
    // How each reason for a missing token is handled, and how a failure of the box's file I/O is handled (F09, F13)
    public class PlaytestUploaderSessionFailureTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;

        [SetUp]
        public void CreateRoot()
        {
            PlaytestUploadRunner.ResetOnPlayMode();
            _root = Path.Combine(Path.GetTempPath(), "playtest-upload-session-" + Path.GetRandomFileName());
            _directories = PlaytestOutboxTestBoxes.Directories(_root);
        }

        [TearDown]
        public void DeleteRoot()
        {
            Directory.Delete(_root, true);
        }

        // チケット拒否は待っても直らずどの箱も同じ。約100秒の再試行も箱の計数もせず走行を止める
        // A rejected ticket never heals and hits every box alike; the run stops without the ~100s of retries or counting a box
        [TestCase(401, "{\"reason\":\"invalid-ticket\"}")]
        public void セッションが拒まれたら再試行も計数もせず走行を止める(int statusCode, string body)
        {
            var first = MakeBox("20260913_110000_aaaa");
            var second = MakeBox("20260913_120000_bbbb");
            var api = new FakeUploadApi { SessionResult = PlaytestApiResult.Responded(statusCode, body) };

            Assert.AreEqual(0, Upload(api));
            Assert.AreEqual(1, api.SessionCallCount);
            CollectionAssert.IsEmpty(api.Calls);
            Assert.IsFalse(File.Exists(Path.Combine(first, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(second, PlaytestOutboxScanner.AttemptsMarker)));
        }

        [Test]
        public void 受け口に届かなければ再試行表を使ってから数えずに走行を止める()
        {
            var first = MakeBox("20260913_110000_aaaa");
            MakeBox("20260913_120000_bbbb");
            var api = new FakeUploadApi { SessionResult = PlaytestApiResult.TransportFailure("offline") };

            Assert.AreEqual(0, Upload(api));
            Assert.AreEqual(1 + PlaytestUploadRetrySchedule.Default.Delays.Count, api.SessionCallCount, "2箱目には進まない");
            Assert.IsFalse(File.Exists(Path.Combine(first, PlaytestOutboxScanner.AttemptsMarker)));
        }

        // 200の本文が壊れたセッションは到達不能と混ぜず契約違反として扱い、走行は止めずに後続の箱も試す
        // A session 200 with a broken body is a contract breach rather than unreachability; the run keeps going and tries later boxes too
        [Test]
        public void セッションの200が契約違反なら数えずに持ち越し後続の箱も試す()
        {
            var first = MakeBox("20260913_110000_aaaa");
            MakeBox("20260913_120000_bbbb");
            var api = new FakeUploadApi { SessionResult = PlaytestApiResult.Responded(200, "<html>captive portal</html>") };

            Assert.AreEqual(0, Upload(api));
            Assert.AreEqual(2 * (1 + PlaytestUploadRetrySchedule.Default.Delays.Count), api.SessionCallCount);
            Assert.IsFalse(File.Exists(Path.Combine(first, PlaytestOutboxScanner.AttemptsMarker)));
        }

        // 箱のファイルの読み書きが例外で止まっても走行を落とさず、試行失敗としてDecideで一過性と判定し数えずに持ち越す
        // An exception reading or writing the box's files never ends the run; it becomes a failed attempt Decide judges transient, deferred uncounted
        [Test]
        public void 箱のファイルの書き込み失敗は試行失敗として再試行され数えずに後続の箱へ進む()
        {
            var broken = MakeBox("20260913_110000_aaaa");
            var later = MakeBox("20260913_120000_bbbb");
            // 宣言の記録先をディレクトリで塞ぎ、書き込みを必ず例外にする
            // The declaration record's path is blocked by a directory so every write throws
            Directory.CreateDirectory(Path.Combine(broken, PlaytestOutboxScanner.DeclaredMarker));
            var api = new FakeUploadApi();

            Assert.AreEqual(1, Upload(api));
            Assert.IsFalse(File.Exists(Path.Combine(broken, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(broken, PlaytestOutboxScanner.UploadedMarker)));
            Assert.IsTrue(File.Exists(Path.Combine(later, PlaytestOutboxScanner.UploadedMarker)));
            CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json", "put:a.bin", "complete" }, api.Calls);
        }

        private string MakeBox(string bundleId)
        {
            return PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, bundleId, ("manifest.json", "{}"), ("a.bin", "abc"));
        }

        private int Upload(FakeUploadApi api)
        {
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var uploader = new PlaytestUploader(api, session, _directories, PlaytestNoWaitRetrySchedule.Create());
            return uploader.UploadPendingAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
