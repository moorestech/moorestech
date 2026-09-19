using System.IO;
using System.Linq;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.BoxFilePolicies;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload;
using Client.PlaytestReceiver.Upload.Attempt;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    // 箱の種類ごとの格付け（F10）。アップローダーは格付けしか見ず、進行記録の唯一のファイルもバグ報告の語彙で補助に落ちない
    // Per-kind rankings (F10); the uploader sees only ranks, and a progress record's single file never falls to supporting under the bug report's vocabulary
    public class PlaytestBoxFilePolicyTest
    {
        [TestCase("manifest.json", PlaytestBundleFileRank.Required)]
        [TestCase("world/world.json", PlaytestBundleFileRank.Required)]
        [TestCase("snapshots/s.json", PlaytestBundleFileRank.Required)]
        [TestCase("frames/frame_0001.jpg", PlaytestBundleFileRank.Frames)]
        [TestCase("frames.tsv", PlaytestBundleFileRank.Supporting)]
        [TestCase("logs/unity.log", PlaytestBundleFileRank.Supporting)]
        public void バグ報告の箱は再現の土台を必須に静止画を最後に置く(string path, PlaytestBundleFileRank expected)
        {
            Assert.AreEqual(expected, new BugReportBoxFilePolicy().RankOf(path));
        }

        [TestCase("record.json", PlaytestBundleFileRank.Required)]
        [TestCase("manifest.json", PlaytestBundleFileRank.Supporting)]
        [TestCase("frames/frame_0001.jpg", PlaytestBundleFileRank.Supporting)]
        public void 進行記録の箱は記録本体だけが必須(string path, PlaytestBundleFileRank expected)
        {
            Assert.AreEqual(expected, new ProgressRecordBoxFilePolicy().RankOf(path));
        }

        // 進行記録の唯一のファイルがR2に拒まれ続けたら、見送って空の箱を送らず箱の失敗として数える
        // When R2 keeps refusing a progress record's single file, it is never skipped into an empty box; the box failure is counted
        [Test]
        public void 進行記録の本体がR2に拒まれ続けたら見送らず箱を1回と数える()
        {
            var root = Path.Combine(Path.GetTempPath(), "playtest-policy-" + Path.GetRandomFileName());
            var directories = PlaytestOutboxTestBoxes.Directories(root);
            var box = PlaytestOutboxTestBoxes.Make(directories.ProgressOutbox, "20260913_120000_aaaa", ("record.json", "{}"));
            var api = new FakeUploadApi();
            var attempts = 1 + PlaytestUploadRetrySchedule.Default.Delays.Count;
            for (var i = 0; i < attempts; i++) api.EnqueuePut("record.json", PlaytestApiResult.Responded(400, "<Error><Code>InvalidArgument</Code></Error>"));

            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var sent = new PlaytestUploader(api, session, directories, PlaytestNoWaitRetrySchedule.Create()).UploadPendingAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, sent);
            Assert.AreEqual(attempts, api.Calls.Count(call => call == "put:record.json"));
            StringAssert.StartsWith("1\n", File.ReadAllText(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.SkippedMarker)));
            Directory.Delete(root, true);
        }
    }
}
