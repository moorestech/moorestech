using System;
using System.Diagnostics;
using System.IO;
using Client.Game.InGame.BugReport;
using Game.Paths;
using NUnit.Framework;

namespace Client.Tests.BugReport.Repository
{
    public class BugReportCommitBundlesTest
    {
        private string _root;
        private string _repository;
        private string _published;
        private string _unpublished;

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../.superpowers/task7-tests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
            Git(_root, "init --bare origin.git");
            Git(_root, "clone origin.git work");
            _repository = Path.Combine(_root, "work");
            Git(_repository, "config user.email test@example.invalid");
            Git(_repository, "config user.name Test");
            Git(_repository, "checkout -b master");
            File.WriteAllText(Path.Combine(_repository, "a.txt"), "first");
            Git(_repository, "add .");
            Git(_repository, "commit -m first");
            Git(_repository, "push -u origin master");
            _published = Git(_repository, "rev-parse HEAD").Trim();
            File.WriteAllText(Path.Combine(_repository, "a.txt"), "second");
            Git(_repository, "commit -am second");
            _unpublished = Git(_repository, "rev-parse HEAD").Trim();
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, true);
        }

        [Test]
        public void UnpublishedCommitCreatesImportableBundleAndRemovesTemporaryRef()
        {
            var manifest = Write(_unpublished);
            File.WriteAllText(Path.Combine(_root, "box/commits.bundle.lock"), "another writer's final-path lock");
            manifest = Write(_unpublished);
            Assert.That(Status(), Does.Contain("commits.bundle: created"));
            Assert.That(Status(), Does.Contain("master-commits.bundle: skipped-no-commit"));
            var destination = Path.Combine(_root, "import");
            Directory.CreateDirectory(destination);
            Git(destination, "init");

            // 空repoでも報告SHAを復元し、受信側の履歴に依存しない
            // Restore the report SHA in an empty repository without prerequisite history
            Git(destination, $"fetch \"{Path.Combine(_root, "box/commits.bundle")}\" '+refs/bugreport/*:refs/bugreport/*'".Replace("'", ""));
            Assert.That(Git(destination, "rev-parse FETCH_HEAD").Trim(), Is.EqualTo(_unpublished));
            Assert.That(Git(_repository, "for-each-ref refs/bugreport"), Is.Empty);
            Assert.That(manifest.Missing, Is.Empty);
        }

        [Test]
        public void PublishedCommitNeedsNoBundle()
        {
            Write(_published);
            Assert.That(Status(), Does.Contain("commits.bundle: not-needed-origin-has-commit"));
            Assert.That(File.Exists(Path.Combine(_root, "box/commits.bundle")), Is.False);
        }

        [Test]
        public void MissingCommitSkipsBundle()
        {
            Write(null);
            Assert.That(Status(), Does.Contain("commits.bundle: skipped-no-commit"));
        }

        [Test]
        public void UnknownCommitIsDeclaredMissing()
        {
            var manifest = Write(new string('a', 40));
            Assert.That(Status(), Does.Contain("commits.bundle: failed-commit-not-found"));
            Assert.That(manifest.Missing.Count, Is.EqualTo(1));
            Assert.That(manifest.Missing[0].Item, Is.EqualTo("repo/commits.bundle"));
            Assert.That(Git(_repository, "for-each-ref refs/bugreport"), Is.Empty);
        }

        [Test]
        public void MissingOriginStillCreatesSelfContainedBundle()
        {
            Git(_repository, "update-ref -d refs/remotes/origin/master");
            var manifest = Write(_unpublished);
            Assert.That(Status(), Does.Contain("commits.bundle: created"));
            Assert.That(manifest.Missing, Is.Empty);
            Assert.That(File.Exists(Path.Combine(_root, "box/commits.bundle")), Is.True);
            Assert.That(Git(_repository, "for-each-ref refs/bugreport"), Is.Empty);
        }

        [Test]
        public void IndependentMasterCommitIsImportedAndItsTemporaryRefIsRemoved()
        {
            Git(_root, "clone origin.git master-work");
            var master = Path.Combine(_root, "master-work");
            Git(master, "config user.email test@example.invalid");
            Git(master, "config user.name Test");
            File.WriteAllText(Path.Combine(master, "master.json"), "{}");
            Git(master, "add .");
            Git(master, "commit -m master-only");
            var commit = Git(master, "rev-parse HEAD").Trim();
            var manifest = new BugReportManifest { Repository = new RepositoryState { Commit = _unpublished }, MasterData = new RepositoryState { Commit = commit } };
            BugReportCommitBundles.Write(Path.Combine(_root, "box"), "master-test", manifest, _repository, master);
            Assert.That(Status(), Does.Contain("master-commits.bundle: created"));
            Directory.CreateDirectory(Path.Combine(_root, "import-master"));
            Git(Path.Combine(_root, "import-master"), "init");
            var destination = Path.Combine(_root, "import-master");
            Git(destination, $"fetch \"{Path.Combine(_root, "box/master-commits.bundle")}\" +refs/bugreport/*:refs/bugreport/*");
            Assert.AreEqual(commit, Git(destination, "rev-parse FETCH_HEAD").Trim());
            Assert.That(Git(master, "for-each-ref refs/bugreport"), Is.Empty);
            Assert.That(manifest.Missing, Is.Empty);
        }

        [Test]
        public void LegacyCompletionKeepsCapturedShaInsteadOfCurrentHead()
        {
            var box = Path.Combine(_root, "legacy");
            Directory.CreateDirectory(box);
            var original = new BugReportManifest { IsEditor = true, Repository = new RepositoryState { Commit = _unpublished }, MasterData = new RepositoryState { Commit = _published } };
            File.WriteAllText(Path.Combine(box, "manifest.json"), original.ToJson());
            File.WriteAllText(Path.Combine(_repository, "later.txt"), "later");
            Git(_repository, "add .");
            Git(_repository, "commit -m later");
            Assert.IsNull(BugReportCommitBundles.CompleteExisting(box, _repository, _repository, Path.Combine(box, "UPLOAD_DECLARED")));
            var saved = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(box, "manifest.json")));
            Assert.AreEqual(_unpublished, (string)saved["repository"]["commit"]);
            Assert.That(Git(_repository, $"bundle list-heads \"{Path.Combine(box, "repo/commits.bundle")}\""), Does.StartWith(_unpublished));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DirectLegacyEntryPreservesDeclaredOrAlreadyCompletedBytes(bool completed)
        {
            var box = Path.Combine(_root, "protected-legacy");
            Directory.CreateDirectory(box);
            var path = Path.Combine(box, "manifest.json");
            const string original = "{\"isEditor\":true,\"missing\":[]}";
            File.WriteAllText(path, original);
            var declared = Path.Combine(box, "UPLOAD_DECLARED");
            File.WriteAllText(declared, "fixed declaration");
            if (completed)
            {
                Directory.CreateDirectory(Path.Combine(box, "repo"));
                File.WriteAllText(Path.Combine(box, "repo/bundle-status.txt"), "commits.bundle: not-needed-origin-has-commit\nmaster-commits.bundle: not-needed-origin-has-commit\n");
            }
            var failure = BugReportCommitBundles.CompleteExisting(box, _repository, _repository, declared);
            if (completed) Assert.IsNull(failure);
            else Assert.That(failure, Does.Contain("既宣言"));
            Assert.AreEqual(original, File.ReadAllText(path));
            Assert.AreEqual("fixed declaration", File.ReadAllText(declared));
            Assert.False(File.Exists(Path.Combine(box, "repo/commits.bundle")));
        }

        private BugReportManifest Write(string commit)
        {
            var manifest = new BugReportManifest { Repository = new RepositoryState { Commit = commit } };
            BugReportCommitBundles.Write(Path.Combine(_root, "box"), "test", manifest, _repository, null);
            return manifest;
        }

        private string Status()
        {
            return File.ReadAllText(Path.Combine(_root, "box", BugReportBundleLayout.BundleStatusFileName));
        }

        private static string Git(string directory, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            var errors = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.Zero, arguments + ": " + errors.Result);
            return output;
        }
    }
}
