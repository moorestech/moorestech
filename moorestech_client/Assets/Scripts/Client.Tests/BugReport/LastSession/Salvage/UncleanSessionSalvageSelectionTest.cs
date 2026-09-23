using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Game.Paths;
using NUnit.Framework;

namespace Client.Tests.BugReport.Salvage
{
    // 複数のセッションが落ちた起動で、最新セッションの所有するスナップショットだけを退避し、どれを採ったかを欠損列に残すことを押さえる（F12・D-C3）
    // Pins that a boot with several crashed sessions salvages only the newest session's owned snapshots and records which one was taken in the missing list (F12, D-C3)
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
        public void 複数の異常終了があれば最新セッションの所有するスナップショットだけを退避し採った出所を表明する()
        {
            var artifacts = PreviousSessionSalvage.Salvage(Request(Crashed(1234, "session_100", _olderWorldSnapshots), Crashed(5678, "session_900", _newestWorldSnapshots)));

            // どのセッションの出所を載せたかが報告に残っていないと、読み手が別セッションの資料と取り違える
            // Unless the report says whose origin was carried, a reader can mistake it for another session's evidence
            var originReasons = artifacts.Missing.FindAll(missing => missing.Item == "previousOrigin");
            Assert.AreEqual(1, originReasons.Count, MissingReasons(artifacts));
            StringAssert.Contains("pid 5678", originReasons[0].Reason);

            // 退避したのは最新セッションが所有印を残したワールドだけ。古い方は元の場所に残る
            // Only the world the newest session marked as owned was salvaged; the older one stays where it was
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.SnapshotsDirectory, "tick_900.json")));
            Assert.IsTrue(File.Exists(Path.Combine(_olderWorldSnapshots, "tick_100.json")));
        }

        // 同じ保存先を後のセッションが使い直すと所有印は後の方になる。最新セッションの印と一致するので退避される
        // When a later session reuses the same directory the ownership mark belongs to it, which matches the newest session so the files are salvaged
        [Test]
        public void 同じワールドで複数回落ちていれば最新セッションの所有印で退避する()
        {
            var artifacts = PreviousSessionSalvage.Salvage(Request(Crashed(1234, "session_100", _newestWorldSnapshots), Crashed(5678, "session_900", _newestWorldSnapshots)));

            var snapshotReasons = artifacts.Missing.FindAll(missing => missing.Item == BugReportBundleLayout.SnapshotDirectoryName);
            Assert.AreEqual(0, snapshotReasons.Count, MissingReasons(artifacts));
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.SnapshotsDirectory, "tick_900.json")));
        }

        // 各セッションはスナップショット開始時に保存先へ所有印を書く。引数順に書くので後に渡したものが所有者として残る
        // Each session writes an ownership mark into its directory when snapshots start; marks are written in argument order so the later one remains the owner
        private static PreviousProcessSession Crashed(int processId, string sessionName, string worldSnapshotDirectory)
        {
            var origin = new SessionOriginSnapshot(null, BuildOriginReading.Editor(), SessionSnapshotCapture.Started(worldSnapshotDirectory, processId, sessionName));
            origin.WriteTo(Path.Combine(worldSnapshotDirectory, WorldDataDirectory.SnapshotOwnerFileName));
            return new PreviousProcessSession
            {
                ProcessId = processId,
                SessionName = sessionName,
                ExitedCleanly = false,
                RecordingDirectory = null,
                Origin = origin,
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
