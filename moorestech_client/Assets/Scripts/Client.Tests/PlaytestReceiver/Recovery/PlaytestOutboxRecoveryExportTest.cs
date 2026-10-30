using System;
using System.IO;
using Client.PlaytestReceiver.BoxFilePolicies;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.PlaytestReceiver.Upload;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Recovery
{
    public class PlaytestOutboxRecoveryExportTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;

        private sealed class FixedDestination : IPlaytestExportDestination
        {
            private readonly string _directory;
            public FixedDestination(string directory) { _directory = directory; }
            public string ResolveDirectory() { return _directory; }
        }

        private sealed class RecordingReveal : IPlaytestFileReveal
        {
            public string Revealed { get; private set; }
            public bool Reveal(string filePath) { Revealed = filePath; return true; }
        }

        private sealed class NoopRequester : Client.Game.InGame.BugReport.Submit.IPlaytestUploadRequester
        {
            public void RequestUpload() { }
        }

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "recovery-" + Path.GetRandomFileName());
            _directories = new PlaytestOutboxDirectories(Path.Combine(_root, "reports"), new BugReportBoxFilePolicy(), Path.Combine(_root, "progress"), new ProgressRecordBoxFilePolicy());
            PlaytestExportStatusBoard.ResetOnPlayMode();
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [Test]
        public void SuccessfulExportPublishesDoneAndRevealsZip()
        {
            var box = Directory.CreateDirectory(Path.Combine(_root, "reports", "a")).FullName;
            File.WriteAllText(Path.Combine(box, "READY"), "");
            var desktop = Directory.CreateDirectory(Path.Combine(_root, "desktop")).FullName;
            var reveal = new RecordingReveal();
            var recovery = new PlaytestOutboxRecovery(new NoopRequester(), _directories, new FixedDestination(desktop), reveal);

            var zipPath = Path.Combine(desktop, "moorestech-reports-20261027-200000.zip");
            recovery.FinishExport(recovery.PackUnsent(PlaytestOutboxScope.ReportsAndProgress, zipPath, DateTime.UtcNow), zipPath);

            var done = (PlaytestExportDone)PlaytestExportStatusBoard.Current.Value;
            Assert.AreEqual(1, done.BoxCount);
            Assert.IsTrue(done.Revealed);
            Assert.AreEqual(zipPath, reveal.Revealed);
            Assert.IsTrue(File.Exists(zipPath));
            Assert.IsTrue(File.Exists(Path.Combine(box, "READY")), "書き出し後も箱は残る");
        }

        [Test]
        public void UnwritableDestinationFailsAndLeavesNoPartialZip()
        {
            var box = Directory.CreateDirectory(Path.Combine(_root, "reports", "a")).FullName;
            File.WriteAllText(Path.Combine(box, "READY"), "");
            // 書き出し先が存在しないディレクトリなら zip を作れない
            // A destination directory that does not exist cannot receive the zip
            var zipPath = Path.Combine(_root, "no-such-dir", "out.zip");
            var recovery = new PlaytestOutboxRecovery(new NoopRequester(), _directories, new FixedDestination(_root), new RecordingReveal());
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("export failed"));

            recovery.FinishExport(recovery.PackUnsent(PlaytestOutboxScope.ReportsAndProgress, zipPath, DateTime.UtcNow), zipPath);

            Assert.IsInstanceOf<PlaytestExportFailed>(PlaytestExportStatusBoard.Current.Value);
            Assert.IsFalse(File.Exists(zipPath));
        }

        [Test]
        public void MissingDesktopFailsBeforeRunning()
        {
            var recovery = new PlaytestOutboxRecovery(new NoopRequester(), _directories, new FixedDestination(null), new RecordingReveal());
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("export failed"));
            Assert.IsNull(recovery.BeginExport());
            Assert.IsInstanceOf<PlaytestExportFailed>(PlaytestExportStatusBoard.Current.Value);
        }

        [Test]
        public void SecondBeginWhileRunningIsIgnored()
        {
            var desktop = Directory.CreateDirectory(Path.Combine(_root, "desktop")).FullName;
            var recovery = new PlaytestOutboxRecovery(new NoopRequester(), _directories, new FixedDestination(desktop), new RecordingReveal());
            Assert.IsNotNull(recovery.BeginExport());
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("already running"));
            Assert.IsNull(recovery.BeginExport());
            Assert.IsInstanceOf<PlaytestExportRunning>(PlaytestExportStatusBoard.Current.Value);
        }

        [Test]
        public void AbandonedRunningFallsBackToFailed()
        {
            // 想定外の例外で走行が抜けても、ボタンが押せないまま固まらない
            // An unexpected exception leaving the run must not freeze the buttons in the running state
            var desktop = Directory.CreateDirectory(Path.Combine(_root, "desktop")).FullName;
            var recovery = new PlaytestOutboxRecovery(new NoopRequester(), _directories, new FixedDestination(desktop), new RecordingReveal());
            recovery.BeginExport();
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("export failed"));
            PlaytestOutboxRecovery.FailIfStillRunning();
            Assert.IsInstanceOf<PlaytestExportFailed>(PlaytestExportStatusBoard.Current.Value);
        }
    }
}
