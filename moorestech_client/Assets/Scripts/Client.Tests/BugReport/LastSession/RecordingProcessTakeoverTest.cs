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
        private const string OlderSessionName = "session_100";
        private const string CurrentSessionName = "session_200";

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
        public void 死んだpidだけを引き継ぎ生存pidと自分の今回のセッションは触らない()
        {
            CreateSessionDirectory(100, OlderSessionName);
            CreateSessionDirectory(200, OlderSessionName);
            CreateSessionDirectory(300, CurrentSessionName);

            var takeover = RecordingProcessDirectories.SelectTakeover(_recording, 300, CurrentSessionName, new[] { 200, 300 });

            Assert.AreEqual(1, takeover.Directories.Count);
            Assert.AreEqual(100, takeover.Directories[0].ProcessId);
            Assert.AreEqual(OlderSessionName, takeover.Directories[0].SessionName);
            Assert.Contains(200, takeover.SkippedLiveProcessIds);
        }

        // 同じpidでの再生し直しは、前のセッションの録画へ書き足さず別の段に書く。旧い段は引き継ぎの対象になる（F05）
        // A same-pid replay writes into a new level instead of appending to the previous session's footage; the older level is taken over (F05)
        [Test]
        public void 自分のpidでも今回以外のセッションは引き継ぐ()
        {
            CreateSessionDirectory(300, OlderSessionName);
            CreateSessionDirectory(300, CurrentSessionName);

            var takeover = RecordingProcessDirectories.SelectTakeover(_recording, 300, CurrentSessionName, new[] { 300 });

            Assert.AreEqual(1, takeover.Directories.Count);
            Assert.AreEqual(OlderSessionName, takeover.Directories[0].SessionName);
            Assert.AreEqual(0, takeover.SkippedLiveProcessIds.Count);
        }

        [Test]
        public void pid以外の名前のディレクトリは引き継がず未知として持ち帰る()
        {
            Directory.CreateDirectory(Path.Combine(_recording, "scratch"));

            var takeover = RecordingProcessDirectories.SelectTakeover(_recording, 300, CurrentSessionName, Array.Empty<int>());

            Assert.AreEqual(0, takeover.Directories.Count);
            Assert.AreEqual(1, takeover.UnknownDirectories.Count);
        }

        [Test]
        public void pid配下のセッション以外の綴りは引き継がず未知として持ち帰る()
        {
            Directory.CreateDirectory(Path.Combine(RecordingProcessDirectories.DirectoryFor(_recording, 100), "live_0000"));

            var takeover = RecordingProcessDirectories.SelectTakeover(_recording, 300, CurrentSessionName, Array.Empty<int>());

            Assert.AreEqual(0, takeover.Directories.Count);
            Assert.AreEqual(1, takeover.UnknownDirectories.Count);
        }

        private void CreateSessionDirectory(int processId, string sessionName)
        {
            Directory.CreateDirectory(ProcessSessionScope.SessionDirectoryFor(_recording, processId, sessionName));
        }
    }
}
