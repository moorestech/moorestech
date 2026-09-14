using System;
using System.IO;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    // 引き継ぎ判定は並列worktreeの録画が生き残るかを決める。生存pidを1つでも拾うと、そのセッションの映像が消える
    // The takeover decision is what keeps a parallel worktree's recording alive; picking up one live pid erases that session's footage
    public class RecordingProcessTakeoverTest
    {
        private string _recording;

        [SetUp]
        public void CreateDirectory()
        {
            _recording = Path.Combine(Path.GetTempPath(), $"moorestech-recording-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_recording);
        }

        [TearDown]
        public void DeleteDirectory()
        {
            if (Directory.Exists(_recording)) Directory.Delete(_recording, true);
        }

        [Test]
        public void 死んだpidだけを引き継ぎ生存pidと自分は触らない()
        {
            CreateProcessDirectory(100);
            CreateProcessDirectory(200);
            CreateProcessDirectory(300);

            var takeover = RecordingProcessDirectories.SelectTakeover(_recording, 300, new[] { 200, 300 });

            Assert.AreEqual(1, takeover.Directories.Count);
            Assert.AreEqual(100, takeover.Directories[0].ProcessId);
            Assert.Contains(200, takeover.SkippedLiveProcessIds);
        }

        [Test]
        public void pid以外の名前のディレクトリは引き継がず未知として持ち帰る()
        {
            Directory.CreateDirectory(Path.Combine(_recording, "scratch"));

            var takeover = RecordingProcessDirectories.SelectTakeover(_recording, 300, Array.Empty<int>());

            Assert.AreEqual(0, takeover.Directories.Count);
            Assert.AreEqual(1, takeover.UnknownDirectories.Count);
        }

        private void CreateProcessDirectory(int processId)
        {
            Directory.CreateDirectory(RecordingProcessDirectories.DirectoryFor(_recording, processId));
        }
    }
}
