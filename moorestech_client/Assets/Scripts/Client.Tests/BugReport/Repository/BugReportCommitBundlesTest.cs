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
            Assert.That(Status(), Does.Contain("commits.bundle: created"));
            Assert.That(Status(), Does.Contain("master-commits.bundle: skipped-no-commit"));
            var destination = Path.Combine(_root, "import");
            Directory.CreateDirectory(destination);
            Git(destination, "init");

            // 差分bundleの前提コミットを先に取り込む。再現側もorigin/masterを取得してからbundleを読む
            // Import the incremental bundle prerequisite first, matching the receiver fetching origin/master before the bundle
            Git(destination, $"fetch \"{Path.Combine(_root, "origin.git")}\" master");
            Git(destination, $"fetch \"{Path.Combine(_root, "box/commits.bundle")}\" '+refs/bugreport/*:refs/bugreport/*'".Replace("'", ""));
            Assert.That(Git(destination, "rev-parse refs/bugreport/test").Trim(), Is.EqualTo(_unpublished));
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
        public void MissingOriginFailsWithoutLeavingRefOrBundle()
        {
            Git(_repository, "update-ref -d refs/remotes/origin/master");
            var manifest = Write(_unpublished);
            Assert.That(Status(), Does.Contain("commits.bundle: failed-bundle-create"));
            Assert.That(manifest.Missing.Count, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(_root, "box/commits.bundle")), Is.False);
            Assert.That(Git(_repository, "for-each-ref refs/bugreport"), Is.Empty);
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
