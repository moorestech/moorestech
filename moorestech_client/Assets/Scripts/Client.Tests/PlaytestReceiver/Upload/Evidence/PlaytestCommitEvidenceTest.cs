using System;
using System.IO;
using System.Threading;
using Client.PlaytestReceiver.Credentials;
using Client.PlaytestReceiver.Upload.Attempt;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestCommitEvidenceTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;

        [SetUp]
        public void Initialize()
        {
            _root = Path.Combine(Path.GetTempPath(), "commit-evidence-" + Guid.NewGuid().ToString("N"));
            _directories = PlaytestOutboxTestBoxes.Directories(_root);
            PlaytestUploadRunner.ResetOnPlayMode();
        }

        [TearDown]
        public void Cleanup()
        {
            PlaytestUploadRunner.ResetOnPlayMode();
            Directory.Delete(_root, true);
        }

        [TestCase("commits.bundle")]
        [TestCase("master-commits.bundle")]
        public void 単体上限を超えるコミット資料は分割して全て送る(string name)
        {
            var box = Make();
            WriteSparse(Path.Combine(box, "repo", name), PlaytestReceiverConfig.MaxFileBytes + 1);
            var api = new FakeUploadApi();
            new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb")).RequestUploadWith(new PlaytestDeveloperTokenRoute("token"));
            Assert.AreEqual(4, api.CompleteCount);
            Assert.True(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
            Assert.That(api.LastPreparedPaths, Does.Contain("repo/commit-transfer.json"));
            Assert.That(api.LastPreparedPaths, Does.Not.Contain("repo/" + name));
        }

        [Test]
        public void 合計上限でもコミット分割後に必須ワールドを保持して送る()
        {
            var box = Make();
            WriteSparse(Path.Combine(box, "world/save.json"), PlaytestReceiverConfig.MaxFileBytes);
            WriteSparse(Path.Combine(box, "repo/commits.bundle"), PlaytestReceiverConfig.MaxFileBytes);
            WriteSparse(Path.Combine(box, "repo/master-commits.bundle"), PlaytestReceiverConfig.MaxBundleBytes - 2 * PlaytestReceiverConfig.MaxFileBytes + 1);
            var declaration = PlaytestBoxDeclaration.Build(PlaytestOutboxScanner.ScanPending(_directories, PlaytestOutboxScope.ReportsOnly)[0]);
            Assert.That(declaration.MissingRequiredReason, Does.Contain("world/save.json"));
            var api = new FakeUploadApi();
            var session = new PlaytestSession(api, new DeveloperTokenSessionCredential("token"));
            var uploader = new PlaytestUploader(api, session, _directories, PlaytestNoWaitRetrySchedule.Create(), PlaytestOutboxScope.ReportsOnly);
            Assert.AreEqual(1, uploader.UploadPendingAsync(CancellationToken.None).GetAwaiter().GetResult().Sent);
            Assert.AreEqual(3, api.CompleteCount);
            Assert.That(api.LastPreparedPaths, Does.Contain("world/save.json").And.Contain("repo/commit-transfer.json"));
        }

        [Test]
        public void 既宣言の欠損は元manifestを変更せず配送不能の印へ残す()
        {
            var box = Make();
            WriteSparse(Path.Combine(box, "repo/commits.bundle"), PlaytestReceiverConfig.MaxFileBytes + 1);
            File.WriteAllText(Path.Combine(box, PlaytestOutboxScanner.DeclaredMarker), "generation\t1\nmanifest.json\n");
            var before = File.ReadAllBytes(Path.Combine(box, "manifest.json"));
            var api = new FakeUploadApi();
            new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb")).RequestUploadWith(new PlaytestDeveloperTokenRoute("token"));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(box, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(box, PlaytestOutboxScanner.FailedMarker)));
            Assert.AreEqual(0, api.CompleteCount);
            Assert.IsEmpty(api.Calls);
            Assert.False(File.Exists(Path.Combine(box, "repo/commit-transfer.json")));
        }

        [TestCase("commits.bundle", "marker")]
        [TestCase("master-commits.bundle", "marker")]
        [TestCase("commits.bundle", "manifest")]
        [TestCase("master-commits.bundle", "manifest")]
        [TestCase("commits.bundle", "attempt")]
        [TestCase("master-commits.bundle", "attempt")]
        public void 記録失敗後の再試行もHTTPへ進まない(string name, string failure)
        {
            var box = Make();
            File.Delete(Path.Combine(box, "repo", name));
            if (failure == "manifest") File.WriteAllText(Path.Combine(box, "manifest.json"), "{\"missing\":42}");
            if (failure == "marker") Directory.CreateDirectory(Path.Combine(box, PlaytestOutboxScanner.FailedMarker));
            if (failure == "attempt")
            {
                Directory.CreateDirectory(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker));
            }
            var api = new FakeUploadApi();
            var session = new PlaytestSession(api, new DeveloperTokenSessionCredential("token"));
            var uploader = new PlaytestUploader(api, session, _directories, PlaytestNoWaitRetrySchedule.Create(), PlaytestOutboxScope.ReportsOnly);
            Assert.AreEqual(0, uploader.UploadPendingAsync(CancellationToken.None).GetAwaiter().GetResult().Sent);
            Assert.IsEmpty(api.Calls);
            Assert.False(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [TestCase("commits.bundle")]
        [TestCase("master-commits.bundle")]
        [TestCase("legacy")]
        public void 消失と旧箱は試行印保存失敗と印解除後にも保留する(string name)
        {
            var box = Make();
            if (name == "legacy")
            {
                File.Delete(Path.Combine(box, "repo/bundle-status.txt"));
                File.WriteAllText(Path.Combine(box, "manifest.json"), "{\"isEditor\":true,\"missing\":[]}");
            }
            else File.Delete(Path.Combine(box, "repo", name));
            Directory.CreateDirectory(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker));
            var api = new FakeUploadApi();
            var session = new PlaytestSession(api, new DeveloperTokenSessionCredential("token"));
            var uploader = new PlaytestUploader(api, session, _directories, PlaytestNoWaitRetrySchedule.Create(), PlaytestOutboxScope.ReportsOnly);
            Assert.AreEqual(0, uploader.UploadPendingAsync(CancellationToken.None).GetAwaiter().GetResult().Sent);
            Directory.Delete(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker));
            File.Delete(Path.Combine(box, PlaytestOutboxScanner.FailedMarker));
            Assert.AreEqual(0, uploader.UploadPendingAsync(CancellationToken.None).GetAwaiter().GetResult().Sent);
            Assert.IsEmpty(api.Calls);
            Assert.True(File.Exists(Path.Combine(box, PlaytestOutboxScanner.FailedMarker)));
            if (name == "legacy") Assert.False(File.Exists(Path.Combine(box, "repo/bundle-status.txt")));
            else Assert.That(File.ReadAllText(Path.Combine(box, "repo/bundle-status.txt")), Does.Contain(name + ": created"));
        }

        [TestCase("commits.bundle")]
        [TestCase("master-commits.bundle")]
        public void 作成済み資料の消失も保留する(string name)
        {
            var box = Make();
            File.Delete(Path.Combine(box, "repo", name));
            AssertHeld(box, name);
        }

        [Test]
        public void 資料未補完のEditor箱だけ保留し正常箱は送る()
        {
            var box = Make();
            File.Delete(Path.Combine(box, "repo/bundle-status.txt"));
            File.WriteAllText(Path.Combine(box, "manifest.json"), "{\"isEditor\":true,\"missing\":[]}");
            var normal = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20261016_130000_bbbb", ("manifest.json", "{\"isEditor\":false,\"missing\":[]}"));
            var api = new FakeUploadApi();
            new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb")).RequestUploadWith(new PlaytestDeveloperTokenRoute("token"));
            Assert.False(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
            Assert.True(File.Exists(Path.Combine(box, PlaytestOutboxScanner.FailedMarker)));
            Assert.True(File.Exists(Path.Combine(normal, PlaytestOutboxScanner.UploadedMarker)));
            Assert.AreEqual(1, api.CompleteCount);
        }

        [Test]
        public void 必須資料は補助ログより先に宣言枠へ入る()
        {
            var box = Make();
            WriteSparse(Path.Combine(box, "world/save.json"), 1024 * 1024);
            WriteSparse(Path.Combine(box, "repo/commits.bundle"), 80L * 1024 * 1024);
            WriteSparse(Path.Combine(box, "logs/a.log"), PlaytestReceiverConfig.MaxFileBytes);
            WriteSparse(Path.Combine(box, "logs/b.log"), PlaytestReceiverConfig.MaxFileBytes);
            var declaration = PlaytestBoxDeclaration.Build(PlaytestOutboxScanner.ScanPending(_directories, PlaytestOutboxScope.ReportsOnly)[0]);
            Assert.IsNull(declaration.MissingRequiredReason);
            Assert.That(declaration.Skipped, Has.Some.Matches<PlaytestSkippedFile>(file => file.Path == "logs/b.log"));
        }

        private string Make()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20261016_120000_aaaa", ("manifest.json", "{\"missing\":[]}"));
            Directory.CreateDirectory(Path.Combine(box, "repo"));
            WriteSparse(Path.Combine(box, "repo/commits.bundle"), 1);
            WriteSparse(Path.Combine(box, "repo/master-commits.bundle"), 1);
            File.WriteAllText(Path.Combine(box, "repo/bundle-status.txt"), "commits.bundle: created\nmaster-commits.bundle: created\n");
            return box;
        }

        private void AssertHeld(string box, string name)
        {
            var api = new FakeUploadApi();
            new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb")).RequestUploadWith(new PlaytestDeveloperTokenRoute("token"));
            Assert.AreEqual(0, api.CompleteCount);
            Assert.False(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
            Assert.AreEqual(1, ((PlaytestOutboxDeliveryCounts)((PlaytestDeliveryPending)PlaytestDeliveryStatusBoard.Current.Value).Inventory).FailedCount);
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(box, "manifest.json")));
            Assert.That(manifest["missing"].ToString(), Does.Contain("repo/" + name).And.Contain("配送不能"));
            var status = File.ReadAllText(Path.Combine(box, "repo/bundle-status.txt"));
            Assert.That(status, Does.Contain("delivery/" + name + ": failed-delivery"));
            CollectionAssert.Contains(status.Split('\n'), name + ": created");
        }

        private static void WriteSparse(string path, long length)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var stream = File.Create(path);
            stream.SetLength(length);
        }
    }
}
