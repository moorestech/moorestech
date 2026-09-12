using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.Recording;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BugReport
{
    public class RecordingRingTest
    {
        private string _root;
        private string _live;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), $"moorestech-ring-{Guid.NewGuid():N}");
            _live = Path.Combine(_root, "live");
            Directory.CreateDirectory(_live);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        // ffmpegを再起動するとlive側はseg_00から上書きされるため、確定分を退避しないと履歴が消える
        // Restarting ffmpeg rewrites live from seg_00, so history is lost unless finished segments are moved out
        // 10秒間隔4本・保持15秒 → 直近2本分(20秒)を確保するため3本残り最古の1本だけ消える
        // 10s-spaced x4, keeping 15s → retains the last 20s (2 gaps) so 3 remain and only the oldest is dropped
        [Test]
        public void 退避すると上書き前の区間が残り保持秒数を満たす本数まで間引かれる()
        {
            for (var i = 0; i < 4; i++) WriteSegment(_live, $"seg_{i:D2}.mp4", i * 10);
            RecordingSegmentRing.PromoteCompletedSegments(_live, _root, 15);

            var retained = Directory.GetFiles(_root, "seg_*.mp4");
            Assert.AreEqual(3, retained.Length);
            Assert.AreEqual(0, Directory.GetFiles(_live, "seg_*.mp4").Length);

            var ordered = RecordingSegmentRing.ListInOrder(_root, _live, true);
            Assert.AreEqual(3, ordered.Count);
            CollectionAssert.AreEqual(new[] { "10", "20", "30" }, new[] { ReadBody(ordered[0]), ReadBody(ordered[1]), ReadBody(ordered[2]) });
        }

        [Test]
        public void 二世代ぶん退避しても名前が衝突せず時刻順に並ぶ()
        {
            WriteSegment(_live, "seg_00.mp4", 0);
            RecordingSegmentRing.PromoteCompletedSegments(_live, _root, GameFrameRecorder.RetentionSeconds);
            WriteSegment(_live, "seg_00.mp4", 1);
            RecordingSegmentRing.PromoteCompletedSegments(_live, _root, GameFrameRecorder.RetentionSeconds);

            var ordered = RecordingSegmentRing.ListInOrder(_root, _live, true);
            Assert.AreEqual(2, ordered.Count);
            Assert.AreEqual("0", ReadBody(ordered[0]));
            Assert.AreEqual("1", ReadBody(ordered[1]));
        }

        // ポーズメニュー連打等で区間が短く切られても、保持秒数(120秒)を下回らないよう本数を伸ばして確保する
        // Even when segments are cut short (e.g. rapid pause-menu opens), the retention window (120s) is never shortchanged
        [Test]
        public void 短い区間が続いても保持秒数を下回らない本数まで残す()
        {
            // 3秒間隔で60本(=180秒ぶん)。本数固定12本では36秒しか残らないが、時間基準なら120秒を満たすまで残す
            // 60 segments spaced 3s apart (180s total); a fixed 12-count would keep only 36s, time-based keeps up to 120s
            for (var i = 0; i < 60; i++) WriteSegment(_live, $"seg_{i:D2}.mp4", i * 3);
            RecordingSegmentRing.PromoteCompletedSegments(_live, _root, GameFrameRecorder.RetentionSeconds);

            var retained = OrderedByWriteTime(_root);
            Assert.Greater(retained.Count, 12, "本数固定だった旧ロジックの上限(12本)を超えて保持できているはず");
            var coveredSeconds = (File.GetLastWriteTimeUtc(retained[^1]) - File.GetLastWriteTimeUtc(retained[0])).TotalSeconds;
            Assert.GreaterOrEqual(coveredSeconds, GameFrameRecorder.RetentionSeconds);
        }

        // 履歴の合計尺が保持秒数に満たない間は、間引かず全区間を残す（120秒に満たなくても消し過ぎない）
        // While total history is shorter than the retention window, nothing is trimmed away
        [Test]
        public void 保持秒数に満たない間は全区間を残す()
        {
            for (var i = 0; i < 3; i++) WriteSegment(_live, $"seg_{i:D2}.mp4", i * 10);
            RecordingSegmentRing.PromoteCompletedSegments(_live, _root, GameFrameRecorder.RetentionSeconds);

            Assert.AreEqual(3, Directory.GetFiles(_root, "seg_*.mp4").Length);
        }

        // 書き込み中の最新live区間はmoovが無く結合できないので一覧から外す
        // The newest live segment has no moov atom yet and would break concat, so it stays out of the listing
        [Test]
        public void 録画中は最新のlive区間を一覧から外す()
        {
            WriteSegment(_live, "seg_00.mp4", 0);
            WriteSegment(_live, "seg_01.mp4", 1);

            Assert.AreEqual(1, RecordingSegmentRing.ListInOrder(_root, _live, true).Count);
            Assert.AreEqual(2, RecordingSegmentRing.ListInOrder(_root, _live, false).Count);
        }

        [Test]
        public void 長さ0の区間は一覧にも退避にも含めない()
        {
            File.WriteAllText(Path.Combine(_live, "seg_00.mp4"), "");
            WriteSegment(_live, "seg_01.mp4", 1);

            RecordingSegmentRing.PromoteCompletedSegments(_live, _root, GameFrameRecorder.RetentionSeconds);
            Assert.AreEqual(1, Directory.GetFiles(_root, "seg_*.mp4").Length);
            Assert.AreEqual(0, Directory.GetFiles(_live, "seg_*.mp4").Length);
        }

        // ffmpegが無くても例外にせず理由を残す。録画が欠けても報告は成立させる設計のため
        // Missing ffmpeg records a reason instead of throwing, because a report stands without its recording
        [Test]
        public void ffmpegが無いときは警告を出して理由を残す()
        {
            LogAssert.Expect(LogType.Warning, $"録画リングを開始しません: {GameFrameRecorder.MissingFfmpegReason}");
            var missing = GameFrameRecorder.ResolveInitialAvailability(null);
            Assert.IsFalse(missing.IsAvailable);
            Assert.AreEqual(GameFrameRecorder.MissingFfmpegReason, missing.Reason);
            Assert.IsTrue(GameFrameRecorder.ResolveInitialAvailability("/opt/homebrew/bin/ffmpeg").IsAvailable);
        }

        // 起動後にffmpegが死ぬと理由がどこにも入らず、報告側が「録れている」枝へ入って古い区間を同梱していた
        // When ffmpeg died after a successful start no reason was set, so the report took the "recording" branch and shipped stale segments
        [Test]
        public void 録画していないときの可用性は必ず理由を持つ()
        {
            var availability = new GameFrameRecorder().Availability;

            Assert.IsFalse(availability.IsAvailable);
            Assert.IsNotEmpty(availability.Reason);
        }

        [Test]
        public void 使えない可用性は理由が空でも既定の理由で埋まる()
        {
            Assert.AreEqual(RecordingAvailability.StoppedWithoutReason, RecordingAvailability.Unavailable("").Reason);
        }

        private static void WriteSegment(string directory, string name, int secondsOffset)
        {
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, secondsOffset.ToString());
            File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(secondsOffset));
        }

        private static string ReadBody(string path)
        {
            return File.ReadAllText(path);
        }

        private static List<string> OrderedByWriteTime(string directory)
        {
            var files = new List<string>(Directory.GetFiles(directory, "seg_*.mp4"));
            files.Sort((a, b) => File.GetLastWriteTimeUtc(a).CompareTo(File.GetLastWriteTimeUtc(b)));
            return files;
        }
    }
}
