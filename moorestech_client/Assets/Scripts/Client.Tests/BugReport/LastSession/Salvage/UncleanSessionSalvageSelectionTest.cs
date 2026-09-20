using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Game.Paths;
using NUnit.Framework;

namespace Client.Tests.BugReport.Salvage
{
    // 複数のセッションが落ちた起動で、退避したものと見送ったものが欠損列に残ることを押さえる（AGENTS.md の fail-closed ログ規約）
    // Pins that a boot with several crashed sessions records both what was salvaged and what was skipped in the missing list (AGENTS.md's fail-closed logging rule)
    public class UncleanSessionSalvageSelectionTest
    {
        private string _root;
        private string _lastSession;
        private string _newestWorldSnapshots;
        private string _olderWorldSnapshots;

        [SetUp]
        public void CreateDirectories()
        {
            _root = Path.Combine(Path.GetTempPath(), $"moorestech-salvage-select-{Guid.NewGuid():N}");
            _lastSession = Path.Combine(_root, "last-session");
            _newestWorldSnapshots = Path.Combine(_root, "newest-world-snapshots");
            _olderWorldSnapshots = Path.Combine(_root, "older-world-snapshots");
            Directory.CreateDirectory(_newestWorldSnapshots);
            Directory.CreateDirectory(_olderWorldSnapshots);
            File.WriteAllText(Path.Combine(_newestWorldSnapshots, "tick_900.json"), "{}");
            File.WriteAllText(Path.Combine(_olderWorldSnapshots, "tick_100.json"), "{}");
        }

        [TearDown]
        public void DeleteDirectories()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void 複数の異常終了があればスナップショットを見送ったセッションも欠損として表明する()
        {
            var artifacts = PreviousSessionSalvage.Salvage(Request(Crashed(1234, "session_100", _olderWorldSnapshots), Crashed(5678, "session_900", _newestWorldSnapshots)));

            // 最新の出所からしか移さない以上、別ワールドの盤面が箱に無い理由が報告に残っていないと読み手が事故に気づけない
            // Since only the newest origin is moved, the report must say why another world's board is absent or nobody notices the loss
            var snapshotReasons = artifacts.Missing.FindAll(missing => missing.Item == BugReportBundleLayout.SnapshotDirectoryName);
            Assert.AreEqual(1, snapshotReasons.Count, MissingReasons(artifacts));
            StringAssert.Contains("見送り", snapshotReasons[0].Reason);
            StringAssert.Contains("pid 5678", snapshotReasons[0].Reason);

            // 退避したのは最新セッションのワールドだけ。古い方は元の場所に残る
            // Only the newest session's world was salvaged; the older one stays where it was
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.SnapshotsDirectory, "tick_900.json")));
            Assert.IsTrue(File.Exists(Path.Combine(_olderWorldSnapshots, "tick_100.json")));
        }

        private static PreviousProcessSession Crashed(int processId, string sessionName, string worldSnapshotDirectory)
        {
            return new PreviousProcessSession
            {
                ProcessId = processId,
                SessionName = sessionName,
                ExitedCleanly = false,
                RecordingDirectory = null,
                Origin = new SessionOriginSnapshot(null, BuildOriginReading.Editor(), new SessionSnapshotSource(false, worldSnapshotDirectory)),
            };
        }

        private PreviousSessionSalvageRequest Request(params PreviousProcessSession[] sessions)
        {
            return new PreviousSessionSalvageRequest
            {
                LastSessionDirectory = _lastSession,
                PreviousSessions = new List<PreviousProcessSession>(sessions),
            };
        }

        private static string MissingReasons(PreviousSessionArtifacts artifacts)
        {
            return string.Join(" / ", artifacts.Missing.ConvertAll(missing => $"{missing.Item}:{missing.Reason}"));
        }
    }
}
