using System;
using System.IO;
using System.Diagnostics;
using Client.Starter.Playtest;
using Client.PlaytestReceiver.Upload;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestExistingReportEvidenceTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;
        private string _box;

        [SetUp]
        public void Initialize()
        {
            _root = Path.Combine(Path.GetTempPath(), "legacy-report-" + Guid.NewGuid().ToString("N"));
            _directories = PlaytestOutboxTestBoxes.Directories(_root);
            _box = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20261016_120000_aaaa",
                ("manifest.json", "{\"isEditor\":true,\"repository\":{\"commit\":\"unknown\"},\"missing\":[]}"));
        }

        [TearDown]
        public void Cleanup() { Directory.Delete(_root, true); }

        [Test]
        public void 不明SHAは元情報を変えず欠損と保留理由を残す()
        {
            PlaytestExistingReportEvidence.Complete(_directories, _root, _root);
            Assert.That(File.ReadAllText(Path.Combine(_box, PlaytestOutboxScanner.FailedMarker)), Does.Contain("SHA"));
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(_box, "manifest.json")));
            Assert.AreEqual("unknown", (string)manifest["repository"]["commit"]);
            Assert.That(manifest["missing"].ToString(), Does.Contain("repo/commit-evidence"));
            Assert.False(File.Exists(Path.Combine(_box, "repo/commits.bundle")));
        }

        [Test]
        public void 既宣言なら元本文と宣言を維持し保留する()
        {
            var declared = Path.Combine(_box, PlaytestOutboxScanner.DeclaredMarker);
            File.WriteAllText(declared, "generation\t1\nmanifest.json\n");
            var before = File.ReadAllText(Path.Combine(_box, "manifest.json"));
            PlaytestExistingReportEvidence.Complete(_directories, _root, _root);
            Assert.AreEqual(before, File.ReadAllText(Path.Combine(_box, "manifest.json")));
            Assert.AreEqual("generation\t1\nmanifest.json\n", File.ReadAllText(declared));
            Assert.That(File.ReadAllText(Path.Combine(_box, PlaytestOutboxScanner.FailedMarker)), Does.Contain("既宣言"));
            Assert.False(Directory.Exists(Path.Combine(_box, "repo")));
        }

        [TestCase("true")]
        [TestCase(null)]
        [TestCase("null")]
        [TestCase("\"unknown\"")]
        [TestCase("42")]
        public void 保留印を書けない旧箱があっても正常箱は送信できる(string editor)
        {
            var manifest = new JObject { ["missing"] = new JArray() };
            if (editor != null) manifest["isEditor"] = JToken.Parse(editor);
            File.WriteAllText(Path.Combine(_box, "manifest.json"), manifest.ToString());
            Directory.CreateDirectory(Path.Combine(_box, PlaytestOutboxScanner.FailedMarker));
            var normal = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20261016_130000_bbbb", ("manifest.json", "{\"isEditor\":false,\"missing\":[]}"));
            PlaytestExistingReportEvidence.Complete(_directories, _root, _root);
            var api = new FakeUploadApi();
            var session = new Client.PlaytestReceiver.PlaytestSession(api, new Client.PlaytestReceiver.Credentials.DeveloperTokenSessionCredential("token"));
            var uploader = new PlaytestUploader(api, session, _directories, PlaytestNoWaitRetrySchedule.Create(), Client.PlaytestReceiver.Delivery.PlaytestOutboxScope.ReportsOnly);
            Assert.AreEqual(1, uploader.UploadPendingAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult().Sent);
            Assert.AreEqual(1, api.CompleteCount);
            Assert.False(File.Exists(Path.Combine(_box, PlaytestOutboxScanner.UploadedMarker)));
            Assert.True(File.Exists(Path.Combine(normal, PlaytestOutboxScanner.UploadedMarker)));
        }

        [TestCase(false, null)]
        [TestCase(true, null)]
        [TestCase(false, "元repoまたはmanifestのSHAを特定できないため送信を保留しました")]
        [TestCase(false, "元SHA aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa を指定repoで確認できません: missing")]
        public void 開発ビルドで保留された旧箱をEditor補完で自動再送できる(bool previousHoldStatus, string legacyReason)
        {
            var repository = Path.Combine(_root, "source");
            Directory.CreateDirectory(repository);
            Git(repository, "init");
            Git(repository, "config user.email test@example.invalid");
            Git(repository, "config user.name Test");
            File.WriteAllText(Path.Combine(repository, "a.txt"), "first");
            Git(repository, "add .");
            Git(repository, "commit -m first");
            Git(repository, "update-ref refs/remotes/origin/master HEAD");
            File.WriteAllText(Path.Combine(repository, "a.txt"), "second");
            Git(repository, "commit -am second");
            var sha = Git(repository, "rev-parse HEAD").Trim();
            var original = new Client.Game.InGame.BugReport.BugReportManifest
            {
                IsEditor = true, Repository = new Client.Game.InGame.BugReport.RepositoryState { Commit = sha },
                MasterData = new Client.Game.InGame.BugReport.RepositoryState { Commit = sha }
            };
            File.WriteAllText(Path.Combine(_box, "manifest.json"), original.ToJson());
            if (previousHoldStatus)
            {
                Directory.CreateDirectory(Path.Combine(_box, "repo"));
                File.WriteAllText(Path.Combine(_box, "repo/bundle-status.txt"), "commit-evidence: failed-delivery (legacy)\n");
            }
            // ビルドの走査で保留された箱もEditor補完で元SHAの資料を作る
            // Editor completion restores original-SHA evidence even after a build scan held the box
            var api = new FakeUploadApi();
            var session = new Client.PlaytestReceiver.PlaytestSession(api, new Client.PlaytestReceiver.Credentials.DeveloperTokenSessionCredential("token"));
            var uploader = new PlaytestUploader(api, session, _directories, PlaytestNoWaitRetrySchedule.Create(), Client.PlaytestReceiver.Delivery.PlaytestOutboxScope.ReportsOnly);
            Assert.AreEqual(0, uploader.UploadPendingAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult().Sent);
            Assert.IsEmpty(api.Calls);
            if (legacyReason != null) File.WriteAllText(Path.Combine(_box, PlaytestOutboxScanner.FailedMarker), legacyReason);
            PlaytestExistingReportEvidence.Complete(_directories, repository, repository);
            Assert.True(File.Exists(Path.Combine(_box, "repo/commits.bundle")));
            Assert.True(File.Exists(Path.Combine(_box, "repo/master-commits.bundle")));
            Assert.AreEqual(1, uploader.UploadPendingAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult().Sent);
            Assert.AreEqual(1, api.CompleteCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 読取不能な保留印を残して正常箱の走査を続ける(bool evidence)
        {
            var marker = Path.Combine(_box, PlaytestOutboxScanner.FailedMarker);
            File.WriteAllText(marker, "unreadable hold");
            var normal = PlaytestOutboxTestBoxes.Make(_directories.ReportOutbox, "20261016_130000_bbbb", ("manifest.json", "{}"));
            using var locked = Environment.OSVersion.Platform == PlatformID.Win32NT ? new FileStream(marker, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
            if (locked == null) SetMode("000");
            // 権限は必ず戻し、失敗箱を配送可能に変えない
            // Always restore permissions without making the failed box eligible for delivery
            try
            {
                var boxes = evidence ? PlaytestOutboxScanner.ScanForEvidence(_directories) :
                    PlaytestOutboxScanner.ScanPending(_directories, Client.PlaytestReceiver.Delivery.PlaytestOutboxScope.ReportsOnly);
                Assert.That(boxes.Count, Is.EqualTo(1));
                Assert.That(boxes[0].Directory, Is.EqualTo(normal));
                Assert.True(File.Exists(marker));
            }
            finally { if (locked == null) SetMode("600"); }

            void SetMode(string mode)
            {
                using var process = Process.Start("/bin/chmod", mode + " " + marker);
                process.WaitForExit();
                Assert.That(process.ExitCode, Is.Zero);
            }
        }

        [TestCase(true, false, false, true)]
        [TestCase(true, true, false, false)]
        [TestCase(true, false, true, false)]
        [TestCase(false, false, false, false)]
        public void 旧形式は未宣言で未補完のEditor箱だけ解除する(bool editor, bool declared, bool completed, bool expected)
        {
            File.WriteAllText(Path.Combine(_box, "manifest.json"), new JObject { ["isEditor"] = editor }.ToString());
            File.WriteAllText(Path.Combine(_box, PlaytestOutboxScanner.FailedMarker), "元repoまたはmanifestのSHAを特定できないため送信を保留しました");
            if (declared) File.WriteAllText(Path.Combine(_box, PlaytestOutboxScanner.DeclaredMarker), "declared");
            if (completed)
            {
                Directory.CreateDirectory(Path.Combine(_box, "repo"));
                File.WriteAllText(Path.Combine(_box, "repo/bundle-status.txt"), "commits.bundle: created\nmaster-commits.bundle: created\n");
            }
            Assert.That(PlaytestOutboxScanner.IsCompletionHold(_box), Is.EqualTo(expected));
        }

        private static string Git(string directory, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
            });
            var errors = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.Zero, arguments + ": " + errors.Result);
            return output;
        }

        [Test]
        public void 配布箱はEditorの資料を足さない()
        {
            File.WriteAllText(Path.Combine(_box, "manifest.json"), "{\"isEditor\":false,\"missing\":[]}");
            PlaytestExistingReportEvidence.Complete(_directories, _root, _root);
            Assert.False(File.Exists(Path.Combine(_box, PlaytestOutboxScanner.FailedMarker)));
            Assert.False(Directory.Exists(Path.Combine(_box, "repo")));
        }
    }
}
