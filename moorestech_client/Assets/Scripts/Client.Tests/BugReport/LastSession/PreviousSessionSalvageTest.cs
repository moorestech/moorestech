using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class PreviousSessionSalvageTest
    {
        private const int DeadProcessId = 1234;
        private const int LiveProcessId = 5678;
        private const string SessionName = "session_100";

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
            File.WriteAllText(Path.Combine(_snapshots, "tick_600.json"), "{}");
            File.WriteAllText(Path.Combine(_snapshots, "packets_601.bin"), "b");
        }

        [TearDown]
        public void DeleteDirectories()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void 異常終了なら自分以外の死んだpidの録画とスナップショットを退避する()
        {
            var deadDirectory = CreateSessionRecording(DeadProcessId);

            var artifacts = PreviousSessionSalvage.Salvage(Request(Session(DeadProcessId, false, deadDirectory)));

            Assert.IsFalse(artifacts.PreviousExitWasClean);
            Assert.Contains(DeadProcessId, (System.Collections.ICollection)artifacts.SalvagedProcessIds);
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.RecordingDirectory, $"pid_{DeadProcessId}", SessionName, "segment-0.mp4")));
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.SnapshotsDirectory, "tick_600.json")));
            Assert.IsTrue(artifacts.HasAnythingToSend);

            // 次のセッションのリングが前回分の上に書かないよう、元は空になり空のpid_<PID>も残らない
            // The originals are emptied, including the emptied pid_<PID>, so the next session's ring never writes on top of them
            Assert.IsFalse(Directory.Exists(deadDirectory));
            Assert.AreEqual(0, Directory.GetDirectories(_recording).Length);
            Assert.AreEqual(0, Directory.GetFiles(_snapshots, "*", SearchOption.AllDirectories).Length);
        }

        [Test]
        public void 生存している他プロセスの録画は退避も削除もせず理由を残す()
        {
            var liveDirectory = CreateSessionRecording(LiveProcessId);
            var request = Request();
            request.SkippedLiveProcessIds.Add(LiveProcessId);

            var artifacts = PreviousSessionSalvage.Salvage(request);

            // 実プレイ中のプロセスの映像を奪うと、そのセッションのバグ報告から動画が黙って消える
            // Stealing a live session's footage would silently erase the video from that session's own bug report
            Assert.IsTrue(File.Exists(Path.Combine(liveDirectory, "segment-0.mp4")));
            StringAssert.Contains($"pid {LiveProcessId}", MissingReasons(artifacts));

            // item名が recording だと「録画が欠けた」と読める。飛ばした理由は専用のitem名で届く
            // The recording name would read as missing footage, so the skip arrives under its own item name
            Assert.AreEqual(PreviousSessionSalvage.LiveProcessMissingItem, artifacts.Missing.Find(missing => missing.Reason.Contains($"pid {LiveProcessId}")).Item);
        }

        [Test]
        public void 正常終了なら死んだpidの録画をディレクトリごと消す()
        {
            var deadDirectory = CreateSessionRecording(DeadProcessId);

            var artifacts = PreviousSessionSalvage.Salvage(Request(Session(DeadProcessId, true, deadDirectory)));

            Assert.IsTrue(artifacts.PreviousExitWasClean);
            Assert.IsNull(artifacts.RecordingDirectory);

            // 空のpid_<PID>を残すと recording/ に積み上がり、起動ごとの全走査が単調に重くなる
            // Leaving an empty pid_<PID> behind piles them up in recording/ and makes every boot's full scan monotonically heavier
            Assert.IsFalse(Directory.Exists(deadDirectory));
            Assert.AreEqual(0, Directory.GetDirectories(_recording).Length);
        }

        [Test]
        public void 正常終了なら前世代の退避物も消して1世代だけ保持する()
        {
            var previousGeneration = Path.Combine(_lastSession, "recording", "pid_777", SessionName);
            Directory.CreateDirectory(previousGeneration);
            File.WriteAllText(Path.Combine(previousGeneration, "segment-0.mp4"), "old");

            PreviousSessionSalvage.Salvage(Request());

            Assert.AreEqual(0, Directory.GetFiles(Path.Combine(_lastSession, "recording"), "*", SearchOption.AllDirectories).Length);
        }

        [Test]
        public void 退避元が空なら前世代の退避物を読み戻して聞き直せる()
        {
            var previousGeneration = Path.Combine(_lastSession, "recording", "pid_777", SessionName);
            Directory.CreateDirectory(previousGeneration);
            File.WriteAllText(Path.Combine(previousGeneration, "segment-0.mp4"), "old");

            var artifacts = PreviousSessionSalvage.Salvage(Request(Session(DeadProcessId, false, null)));

            // 「前回分は last-session に残るので次回起動で聞き直せる」を実際に成立させる経路
            // The path that actually makes "the salvage stays in last-session so the next boot can ask again" true
            Assert.AreEqual(Path.Combine(_lastSession, "recording"), artifacts.RecordingDirectory);
            Assert.IsTrue(File.Exists(Path.Combine(previousGeneration, "segment-0.mp4")));
        }

        [Test]
        public void リモート接続ならスナップショットの不在を退避失敗と書かない()
        {
            var remote = Session(DeadProcessId, false, null);
            remote.Origin = Origin(new SessionSnapshotSource(true, null));

            var artifacts = PreviousSessionSalvage.Salvage(Request(remote));

            Assert.IsNull(artifacts.SnapshotsDirectory);
            StringAssert.Contains("リモート接続", MissingReasons(artifacts));
        }

        // 退避元を記録しない旧版の印。今回の起動の既定ワールドで代用せず、源が分からないことを欠損として表明する（D-C3）
        // An older mark that recorded no source; instead of standing in with this boot's default world, the unknown source is declared missing (D-C3)
        [Test]
        public void 退避元の記録が無い前回セッションはスナップショット源不明として表明する()
        {
            var withoutSource = Session(DeadProcessId, false, null);
            withoutSource.Origin = Origin(null);

            var artifacts = PreviousSessionSalvage.Salvage(Request(withoutSource));

            Assert.IsNull(artifacts.SnapshotsDirectory);
            StringAssert.Contains("スナップショット源不明", MissingReasons(artifacts));
            Assert.AreEqual(2, Directory.GetFiles(_snapshots, "*", SearchOption.AllDirectories).Length, "源が不明なのに今回の既定ワールドから退避している");
        }

        // 前回セッションが記録した退避元だけを使う。今回の起動が別ワールドでも、落ちたセッションのスナップショットが箱へ入る（D-C3）
        // Only the source the previous session recorded is used, so the crashed session's snapshots reach the box even when this boot uses another world (D-C3)
        [Test]
        public void 退避元は前回セッションが記録したワールドから決まる()
        {
            var otherWorldSnapshots = Path.Combine(_root, "generated-world-snapshots");
            Directory.CreateDirectory(otherWorldSnapshots);
            File.WriteAllText(Path.Combine(otherWorldSnapshots, "tick_900.json"), "{}");
            var crashed = Session(DeadProcessId, false, null);
            crashed.Origin = Origin(new SessionSnapshotSource(false, otherWorldSnapshots));

            var artifacts = PreviousSessionSalvage.Salvage(Request(crashed));

            Assert.IsTrue(File.Exists(Path.Combine(artifacts.SnapshotsDirectory, "tick_900.json")));
            Assert.AreEqual(2, Directory.GetFiles(_snapshots, "*", SearchOption.AllDirectories).Length, "前回セッションが遊んでいないワールドのスナップショットを退避している");
        }

        // 「初回起動を異常終了にしない」は印が1件も無いことから出る。印が1件でも残れば異常終了へ倒れる
        // "A first boot is not a crash" follows from there being no marks at all; a single leftover mark tips it to unclean
        [Test]
        public void 前回セッションが1件も無ければ異常終了として扱わない()
        {
            Assert.IsTrue(PreviousSessionSalvage.Salvage(Request()).PreviousExitWasClean);
            Assert.IsFalse(PreviousSessionSalvage.Salvage(Request(Session(DeadProcessId, false, null))).PreviousExitWasClean);
        }

        private string CreateSessionRecording(int processId)
        {
            var directory = ProcessSessionScope.SessionDirectoryFor(_recording, processId, SessionName);
            Directory.CreateDirectory(Path.Combine(directory, "live_0000"));
            File.WriteAllText(Path.Combine(directory, "segment-0.mp4"), "video");
            return directory;
        }

        // 既定では前回セッションが今回と同じワールドを遊んでいた形。退避元を変える検証だけが Origin を差し替える
        // By default the previous session played the same world as this boot; only the tests about the source replace the Origin
        private PreviousProcessSession Session(int processId, bool exitedCleanly, string recordingDirectory)
        {
            return new PreviousProcessSession
            {
                ProcessId = processId,
                SessionName = SessionName,
                ExitedCleanly = exitedCleanly,
                RecordingDirectory = recordingDirectory,
                Origin = Origin(new SessionSnapshotSource(false, _snapshots)),
            };
        }

        private static SessionOriginSnapshot Origin(SessionSnapshotSource snapshotSource)
        {
            return new SessionOriginSnapshot(null, BuildOriginReading.Editor(), snapshotSource);
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
