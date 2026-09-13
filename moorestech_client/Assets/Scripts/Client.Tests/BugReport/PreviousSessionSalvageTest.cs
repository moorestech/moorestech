using System;
using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class PreviousSessionSalvageTest
    {
        private string _root;
        private string _recording;
        private string _snapshots;
        private string _lastSession;

        [SetUp]
        public void CreateDirectories()
        {
            _root = Path.Combine(Path.GetTempPath(), $"moorestech-salvage-{Guid.NewGuid():N}");
            _recording = Path.Combine(_root, "recording");
            _snapshots = Path.Combine(_root, "snapshots");
            _lastSession = Path.Combine(_root, "last-session");
            Directory.CreateDirectory(_recording);
            Directory.CreateDirectory(_snapshots);
            File.WriteAllText(Path.Combine(_recording, "seg_00.mp4"), "video");
            File.WriteAllText(Path.Combine(_snapshots, "tick_600.json"), "{}");
            File.WriteAllText(Path.Combine(_snapshots, "packets_601.bin"), "b");
        }

        [TearDown]
        public void DeleteDirectories()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void 異常終了なら録画とスナップショットを退避する()
        {
            var artifacts = PreviousSessionSalvage.Salvage(false, _recording, _snapshots, _lastSession);

            Assert.IsFalse(artifacts.PreviousExitWasClean);
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.RecordingDirectory, "seg_00.mp4")));
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.SnapshotsDirectory, "tick_600.json")));
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.SnapshotsDirectory, "packets_601.bin")));
            Assert.IsTrue(artifacts.HasAnythingToSend);

            // 次のセッションのリングが前回分の上に書かないよう、元は空になっている
            // The originals are emptied so the next session's ring never writes on top of them
            Assert.AreEqual(0, Directory.GetFiles(_recording).Length);
            Assert.AreEqual(0, Directory.GetFiles(_snapshots).Length);
        }

        [Test]
        public void 正常終了なら退避せず元を消す()
        {
            var artifacts = PreviousSessionSalvage.Salvage(true, _recording, _snapshots, _lastSession);

            Assert.IsTrue(artifacts.PreviousExitWasClean);
            Assert.IsNull(artifacts.RecordingDirectory);
            Assert.IsNull(artifacts.SnapshotsDirectory);
            Assert.AreEqual(0, Directory.GetFiles(_recording).Length);

            // スナップショットはサーバーのリングが世代管理するので消さない
            // Snapshots stay because the server ring manages their generations
            Assert.AreEqual(2, Directory.GetFiles(_snapshots).Length);
        }

        [Test]
        public void 退避元が空でも欠損理由を残して続行する()
        {
            File.Delete(Path.Combine(_recording, "seg_00.mp4"));
            var artifacts = PreviousSessionSalvage.Salvage(false, _recording, "/nonexistent/snapshots", _lastSession);

            Assert.IsNull(artifacts.RecordingDirectory);
            Assert.IsNull(artifacts.SnapshotsDirectory);
            StringAssert.Contains("recording", string.Join(",", artifacts.Missing.ConvertAll(m => m.Item)));
            StringAssert.Contains("snapshots", string.Join(",", artifacts.Missing.ConvertAll(m => m.Item)));
        }
    }
}
