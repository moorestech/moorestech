using System.IO;
using System.Linq;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload;
using Client.PlaytestReceiver.Upload.Attempt;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    // 宣言に何を載せるか（D1・D3）。優先度順・必須群の欠け・R2が拒んだファイルの見送りと、その記録の走行跨ぎ
    // What goes into the declaration (D1, D3): priority order, missing required files, and skipping files R2 refused with the record surviving runs
    public class PlaytestUploadDeclarationScopeTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;

        [SetUp]
        public void CreateRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "playtest-upload-scope-" + Path.GetRandomFileName());
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
        public void 宣言は必須_補助_静止画の順に並ぶ()
        {
            var box = MakeBox(("frames/frame_0001.jpg", "f"), ("logs/unity.log", "l"), ("video.mp4", "v"), ("world/world.json", "w"), ("snapshots/s.json", "s"), ("manifest.json", "{}"), ("frames.tsv", "t"));

            var declaration = PlaytestBoxDeclaration.Build(Box(box));
            var expected = new[] { "manifest.json", "snapshots/s.json", "world/world.json", "frames.tsv", "logs/unity.log", "video.mp4", "frames/frame_0001.jpg" };
            CollectionAssert.AreEqual(expected, declaration.Files.Select(file => file.Path));
        }

        [Test]
        public void 件数上限で落ちるのは静止画だけで箱は送られる()
        {
            var box = MakeBox(("manifest.json", "{}"), ("world/world.json", "w"), ("snapshots/s.json", "s"));
            for (var i = 0; i < PlaytestReceiverConfig.MaxBundleFiles; i++) File.WriteAllText(Path.Combine(box, "frames", $"frame_{i:D4}.jpg"), "f");

            var declaration = PlaytestBoxDeclaration.Build(Box(box));
            Assert.AreEqual(PlaytestReceiverConfig.MaxBundleFiles, declaration.Files.Count);
            Assert.IsNull(declaration.MissingRequiredReason);
            Assert.AreEqual(3, declaration.Skipped.Count);
            Assert.IsTrue(declaration.Skipped.All(file => file.Path.StartsWith("frames/") && file.Reason == "too-many-files"));
        }

        [Test]
        public void 必須群が件数上限に掛かった箱は受け口へ行かず1回と数える()
        {
            var box = MakeBox(("manifest.json", "{}"));
            for (var i = 0; i < PlaytestReceiverConfig.MaxBundleFiles; i++) File.WriteAllText(Path.Combine(box, "snapshots", $"s_{i:D4}.json"), "s");
            var api = new FakeUploadApi();

            Assert.AreEqual(0, Upload(api));
            CollectionAssert.IsEmpty(api.Calls);
            StringAssert.Contains("too-many-files", File.ReadAllText(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
        }

        [Test]
        public void 署名付きPUTの4xxはそのファイルだけ見送り縮小した宣言で送る()
        {
            var box = MakeBox(("manifest.json", "{}"), ("a.bin", "abc"));
            var api = new FakeUploadApi();
            api.EnqueuePut("a.bin", PlaytestApiResult.Responded(400, "<Error><Code>InvalidArgument</Code></Error>"));

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json", "put:a.bin", "prepare", "complete" }, api.Calls);
            CollectionAssert.AreEqual(new[] { "manifest.json" }, api.LastPreparedPaths);
            StringAssert.Contains("{\"path\":\"a.bin\",\"reason\":\"http-400\",\"bytes\":3}", api.LastCompleteBody);
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        // 受け口は縮小した宣言を元へ戻す再宣言を拒むので、持ち越した箱も次の走行で同じファイルを見送り続ける
        // The receiver refuses a re-declaration growing back from a shrunk one, so a deferred box keeps skipping the file on the next run
        [Test]
        public void R2が拒んだファイルの見送りは次回起動の宣言にも残る()
        {
            var box = MakeBox(("manifest.json", "{}"), ("a.bin", "abc"));
            var api = new FakeUploadApi();
            api.EnqueuePut("a.bin", PlaytestApiResult.Responded(400, ""));
            for (var i = 0; i < 4; i++) api.EnqueueComplete(PlaytestApiResult.Responded(503, ""));
            Assert.AreEqual(0, Upload(api));

            api.Calls.Clear();
            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json", "complete" }, api.Calls);
            CollectionAssert.AreEqual(new[] { "manifest.json" }, api.LastPreparedPaths);
            StringAssert.Contains("{\"path\":\"a.bin\",\"reason\":\"http-400\",\"bytes\":3}", api.LastCompleteBody);
        }

        // 必須群は見送りに記録しない。バケット名の誤り等を直せば次の走行で送れる
        // Required files are never recorded as skipped; once e.g. a bucket-name typo is fixed, the next run ships
        [Test]
        public void 必須ファイルがR2に拒まれた箱は見送りを記録せず1回と数え次の走行で送れる()
        {
            var box = MakeBox(("manifest.json", "{}"), ("a.bin", "abc"));
            var api = new FakeUploadApi();
            api.EnqueuePut("manifest.json", PlaytestApiResult.Responded(404, "<Error><Code>NoSuchBucket</Code></Error>"));

            Assert.AreEqual(0, Upload(api));
            CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json" }, api.Calls);
            StringAssert.StartsWith("1\n", File.ReadAllText(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)));
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.SkippedMarker)));

            api.Calls.Clear();
            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(new[] { "manifest.json", "a.bin" }, api.LastPreparedPaths);
        }

        // 上限で前回落ちた静止画が、見送りで空いた枠へ次の走行で入ると宣言の拡大になり受け口に拒まれる
        // A still the caps dropped last run would enter a slot freed by a skip on the next run, growing the declaration the receiver refuses
        [Test]
        public void 前回の宣言に無かったファイルは次の走行の宣言にも入れない()
        {
            var box = MakeBox(("manifest.json", "{}"), ("world/world.json", "w"), ("snapshots/s.json", "s"));
            var frameCount = PlaytestReceiverConfig.MaxBundleFiles - 3 + 1;
            for (var i = 0; i < frameCount; i++) File.WriteAllText(Path.Combine(box, "frames", $"frame_{i:D4}.jpg"), "f");
            var droppedByCap = $"frames/frame_{frameCount - 1:D4}.jpg";
            var api = new FakeUploadApi();
            api.EnqueuePut("frames/frame_0000.jpg", PlaytestApiResult.Responded(400, ""));
            for (var i = 0; i < 4; i++) api.EnqueueComplete(PlaytestApiResult.Responded(503, ""));
            Assert.AreEqual(0, Upload(api));
            var shrunk = api.LastPreparedPaths;
            CollectionAssert.DoesNotContain(shrunk, droppedByCap);

            Assert.AreEqual(1, Upload(api));
            CollectionAssert.AreEqual(shrunk, api.LastPreparedPaths);
            StringAssert.Contains($"{{\"path\":\"{droppedByCap}\",\"reason\":\"outside-earlier-declaration\"", api.LastCompleteBody);
        }

        private string MakeBox(params (string Name, string Content)[] files)
        {
            var box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20260913_120000_aaaa");
            foreach (var directory in new[] { "frames", "logs", "world", "snapshots" }) Directory.CreateDirectory(Path.Combine(box, directory));
            foreach (var file in files) File.WriteAllText(Path.Combine(box, file.Name), file.Content);
            return box;
        }

        private static PlaytestOutboxBox Box(string directory)
        {
            return new PlaytestOutboxBox(directory, Path.GetFileName(directory), PlaytestUploadKind.Report);
        }

        private int Upload(FakeUploadApi api)
        {
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var uploader = new PlaytestUploader(api, session, _directories, PlaytestNoWaitRetrySchedule.Create());
            return uploader.UploadPendingAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
