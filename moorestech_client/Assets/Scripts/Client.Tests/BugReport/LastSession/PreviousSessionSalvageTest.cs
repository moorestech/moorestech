using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class PreviousSessionSalvageTest
    {
        private const int DeadProcessId = 1234;
        private const int LiveProcessId = 5678;
        private const int CurrentProcessId = 4321;

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
            var deadDirectory = CreateProcessRecording(DeadProcessId);

            var artifacts = PreviousSessionSalvage.Salvage(Request(Session(DeadProcessId, false, deadDirectory)));

            Assert.IsFalse(artifacts.PreviousExitWasClean);
            Assert.Contains(DeadProcessId, artifacts.SalvagedProcessIds);
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.RecordingDirectory, $"pid_{DeadProcessId}", "segment-0.mp4")));
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.SnapshotsDirectory, "tick_600.json")));
            Assert.IsTrue(artifacts.HasAnythingToSend);

            // 次のセッションのリングが前回分の上に書かないよう、元は空になっている
            // The originals are emptied so the next session's ring never writes on top of them
            Assert.IsFalse(Directory.Exists(deadDirectory));
            Assert.AreEqual(0, Directory.GetFiles(_snapshots, "*", SearchOption.AllDirectories).Length);
        }

        [Test]
        public void 生存している他プロセスの録画は退避も削除もせず理由を残す()
        {
            var liveDirectory = CreateProcessRecording(LiveProcessId);
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
            var deadDirectory = CreateProcessRecording(DeadProcessId);

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
            var previousGeneration = Path.Combine(_lastSession, "recording", "pid_777");
            Directory.CreateDirectory(previousGeneration);
            File.WriteAllText(Path.Combine(previousGeneration, "segment-0.mp4"), "old");

            PreviousSessionSalvage.Salvage(Request());

            Assert.AreEqual(0, Directory.GetFiles(Path.Combine(_lastSession, "recording"), "*", SearchOption.AllDirectories).Length);
        }

        [Test]
        public void 退避元が空なら前世代の退避物を読み戻して聞き直せる()
        {
            var previousGeneration = Path.Combine(_lastSession, "recording", "pid_777");
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
            var request = Request(Session(DeadProcessId, false, null));
            request.IsRemoteConnection = true;

            var artifacts = PreviousSessionSalvage.Salvage(request);

            Assert.IsNull(artifacts.SnapshotsDirectory);
            StringAssert.Contains("リモート接続", MissingReasons(artifacts));
        }

        // 「初回起動を異常終了にしない」は印が1件も無いことから出る。印が1件でも残れば異常終了へ倒れる
        // "A first boot is not a crash" follows from there being no marks at all; a single leftover mark tips it to unclean
        [Test]
        public void 前回セッションが1件も無ければ異常終了として扱わない()
        {
            Assert.IsTrue(PreviousSessionSalvage.Salvage(Request()).PreviousExitWasClean);
            Assert.IsFalse(PreviousSessionSalvage.Salvage(Request(Session(DeadProcessId, false, null))).PreviousExitWasClean);
        }

        // 常時記録オフやffmpeg不在のEditorは録画ディレクトリを作らない。録画の有無で生存を判定すると、この印が消えて偽のクラッシュになる
        // An Editor with capture off or no ffmpeg creates no recording directory; judging liveness by that would erase its mark and fabricate a crash
        [Test]
        public void 録画が無くても生存しているpidは前回セッションに数えない()
        {
            var scan = PreviousProcessScanner.Scan(CurrentProcessId, new RecordingProcessTakeover(), new[] { LiveProcessId, DeadProcessId }, new[] { LiveProcessId, CurrentProcessId });

            Assert.AreEqual(1, scan.Sessions.Count);
            Assert.AreEqual(DeadProcessId, scan.Sessions[0].ProcessId);
            Assert.Contains(LiveProcessId, scan.SkippedLiveProcessIds);
        }

        // 録画ディレクトリを持つ生存pidも、印だけの生存pidも、同じ1つの生存集合で弾かれる
        // A live pid with a recording directory and one with only a mark are both rejected by the same single liveness set
        [Test]
        public void 自分のpidは印があっても前回セッションにしない()
        {
            var scan = PreviousProcessScanner.Scan(CurrentProcessId, new RecordingProcessTakeover(), new[] { CurrentProcessId }, new[] { CurrentProcessId });

            Assert.AreEqual(0, scan.Sessions.Count);
            Assert.AreEqual(0, scan.SkippedLiveProcessIds.Count);
        }

        private string CreateProcessRecording(int processId)
        {
            var directory = RecordingProcessDirectories.DirectoryFor(_recording, processId);
            Directory.CreateDirectory(Path.Combine(directory, "live_0000"));
            File.WriteAllText(Path.Combine(directory, "segment-0.mp4"), "video");
            return directory;
        }

        private static PreviousProcessSession Session(int processId, bool exitedCleanly, string recordingDirectory)
        {
            return new PreviousProcessSession { ProcessId = processId, ExitedCleanly = exitedCleanly, RecordingDirectory = recordingDirectory };
        }

        private PreviousSessionSalvageRequest Request(params PreviousProcessSession[] sessions)
        {
            return new PreviousSessionSalvageRequest
            {
                WorldSnapshotDirectory = _snapshots,
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
