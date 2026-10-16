using System;
using System.IO;
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
        public void 単体上限を超えるコミット資料は送信成功にしない(string name)
        {
            var box = Make();
            WriteSparse(Path.Combine(box, "repo", name), PlaytestReceiverConfig.MaxFileBytes + 1);
            AssertHeld(box, name);
        }

        [Test]
        public void 合計上限で落ちるマスタ資料も欠損と配送不能にする()
        {
            var box = Make();
            WriteSparse(Path.Combine(box, "world/save.json"), PlaytestReceiverConfig.MaxFileBytes);
            WriteSparse(Path.Combine(box, "repo/commits.bundle"), PlaytestReceiverConfig.MaxFileBytes);
            WriteSparse(Path.Combine(box, "repo/master-commits.bundle"), PlaytestReceiverConfig.MaxBundleBytes - 2 * PlaytestReceiverConfig.MaxFileBytes + 1);
            AssertHeld(box, "master-commits.bundle");
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
        }

        private string Make()
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20261016_120000_aaaa", ("manifest.json", "{\"missing\":[]}"));
            Directory.CreateDirectory(Path.Combine(box, "repo"));
            File.WriteAllText(Path.Combine(box, "repo/bundle-status.txt"), "commits.bundle: created\nmaster-commits.bundle: created\n");
            return box;
        }

        private void AssertHeld(string box, string name)
        {
            var api = new FakeUploadApi();
            new PlaytestUploadRunner(api, _directories, new FakeTicketProvider("aabb")).RequestUploadWith(new PlaytestDeveloperTokenRoute("token"));
            Assert.AreEqual(0, api.CompleteCount);
            Assert.False(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
            Assert.AreEqual(1, ((PlaytestDeliveryPending)PlaytestDeliveryStatusBoard.Current.Value).FailedCount);
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(box, "manifest.json")));
            Assert.That(manifest["missing"].ToString(), Does.Contain("repo/" + name).And.Contain("配送不能"));
            var status = File.ReadAllText(Path.Combine(box, "repo/bundle-status.txt"));
            Assert.That(status, Does.Contain(name + ": failed-delivery"));
            CollectionAssert.DoesNotContain(status.Split('\n'), name + ": created");
        }

        private static void WriteSparse(string path, long length)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var stream = File.Create(path);
            stream.SetLength(length);
        }
    }
}
