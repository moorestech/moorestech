using System;
using System.IO;
using System.Linq;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.BoxFilePolicies;
using Client.PlaytestReceiver.Credentials;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using Client.PlaytestReceiver.Upload.Attempt;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class CommitEvidenceTransferTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;

        [SetUp]
        public void Initialize()
        {
            _root = Path.Combine(Path.GetTempPath(), "commit-transfer-" + Guid.NewGuid().ToString("N"));
            _directories = PlaytestOutboxTestBoxes.Directories(_root);
        }

        [TearDown]
        public void Cleanup() { Directory.Delete(_root, true); }

        [TestCase(false)]
        [TestCase(true)]
        public void 単体超過と箱合計超過のコミット資料を全て再送できる(bool aggregateOnly)
        {
            var directory = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20261016_120000_aaaa",
                ("manifest.json", "{\"isEditor\":true,\"missing\":[]}"));
            Directory.CreateDirectory(Path.Combine(directory, "repo"));
            File.WriteAllText(Path.Combine(directory, "repo/bundle-status.txt"),
                "commits.bundle: created\nmaster-commits.bundle: " + (aggregateOnly ? "created" : "skipped-no-commit"));
            var size = aggregateOnly ? PlaytestReceiverConfig.MaxFileBytes * 9 / 10 : PlaytestReceiverConfig.MaxFileBytes + 1;
            Sparse("repo/commits.bundle", size);
            if (aggregateOnly) { Sparse("repo/master-commits.bundle", size); Sparse("large.bin", size); }
            File.WriteAllText(Path.Combine(directory, PlaytestOutboxScanner.FailedMarker), "repo/commits.bundle: too-large");
            var box = new PlaytestOutboxBox(directory, Path.GetFileName(directory), Client.PlaytestReceiver.Http.PlaytestUploadKind.Report, new BugReportBoxFilePolicy());
            var attachments = box.FilePolicy.PrepareAttachments(box);
            Assert.That(attachments.Count, Is.EqualTo(2));
            foreach (var attachment in attachments)
            {
                var declaration = PlaytestBoxDeclaration.Build(attachment);
                Assert.IsNull(declaration.MissingRequiredReason);
                Assert.IsNull(attachment.FilePolicy.ValidateBeforeUpload(attachment.Directory, declaration));
                Assert.LessOrEqual(declaration.Files.Count, PlaytestReceiverConfig.MaxBundleFiles);
                Assert.LessOrEqual(declaration.Files.Sum(file => file.Bytes), PlaytestReceiverConfig.MaxBundleBytes);
                Assert.That(declaration.Files.All(file => file.Bytes <= PlaytestReceiverConfig.MaxFileBytes));
            }
            var planPath = Path.Combine(directory, "repo/commit-transfer.json");
            var plan = File.ReadAllText(planPath);
            var expectedBytes = aggregateOnly ? size * 2 : size;
            Assert.AreEqual(expectedBytes, JArray.Parse(plan).Sum(bundle => (long)bundle["bytes"]));
            var api = new FakeUploadApi();
            api.EnqueuePrepare(Client.PlaytestReceiver.Http.PlaytestApiResult.TransportFailure("temporary interruption"));
            var session = new PlaytestSession(api, new DeveloperTokenSessionCredential("token"));
            var uploader = new PlaytestUploader(api, session, _directories, PlaytestNoWaitRetrySchedule.Create(), PlaytestOutboxScope.ReportsOnly);
            Assert.AreEqual(0, uploader.UploadPendingAsync(CancellationToken.None).GetAwaiter().GetResult().Sent);
            Assert.AreEqual(1, uploader.UploadPendingAsync(CancellationToken.None).GetAwaiter().GetResult().Sent);
            Assert.False(File.Exists(Path.Combine(directory, PlaytestOutboxScanner.FailedMarker)));
            Assert.AreEqual(plan, File.ReadAllText(planPath));
            Assert.That(api.LastPreparedPaths, Does.Contain("repo/commit-transfer.json"));
            Assert.That(api.LastPreparedPaths, Does.Not.Contain("repo/commits.bundle"));
            Assert.That(api.LastPreparedPaths.All(path => !path.StartsWith("__attachments/")));

            void Sparse(string relative, long length)
            {
                using var stream = File.Create(Path.Combine(directory, relative));
                stream.SetLength(length);
            }
        }

        [Test]
        public void 同じ箱の補完と宣言はロック解放まで拒否する()
        {
            Directory.CreateDirectory(_root);
            using (PlaytestOutboxAccess.AcquireBox(_root))
                Assert.Throws<IOException>(() => { using var blocked = PlaytestOutboxAccess.AcquireBox(_root); });
            using var acquired = PlaytestOutboxAccess.AcquireBox(_root);
            Assert.That(PlaytestOutboxScanner.ListPayloadFiles(_root), Is.Empty);
        }
    }
}
