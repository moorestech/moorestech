using System;
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
        [Test]
        public void 退避すると上書き前の区間が残り保持本数まで間引かれる()
        {
            for (var i = 0; i < 4; i++) WriteSegment(_live, $"seg_{i:D2}.mp4", i);
            RecordingSegmentRing.PromoteCompletedSegments(_live, _root, 3);

            var retained = Directory.GetFiles(_root, "seg_*.mp4");
            Assert.AreEqual(3, retained.Length);
            Assert.AreEqual(0, Directory.GetFiles(_live, "seg_*.mp4").Length);

            var ordered = RecordingSegmentRing.ListInOrder(_root, _live, true);
            Assert.AreEqual(3, ordered.Count);
            CollectionAssert.AreEqual(new[] { "1", "2", "3" }, new[] { ReadBody(ordered[0]), ReadBody(ordered[1]), ReadBody(ordered[2]) });
        }

        [Test]
        public void 二世代ぶん退避しても名前が衝突せず時刻順に並ぶ()
        {
            WriteSegment(_live, "seg_00.mp4", 0);
            RecordingSegmentRing.PromoteCompletedSegments(_live, _root, 12);
            WriteSegment(_live, "seg_00.mp4", 1);
            RecordingSegmentRing.PromoteCompletedSegments(_live, _root, 12);

            var ordered = RecordingSegmentRing.ListInOrder(_root, _live, true);
            Assert.AreEqual(2, ordered.Count);
            Assert.AreEqual("0", ReadBody(ordered[0]));
            Assert.AreEqual("1", ReadBody(ordered[1]));
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

            RecordingSegmentRing.PromoteCompletedSegments(_live, _root, 12);
            Assert.AreEqual(1, Directory.GetFiles(_root, "seg_*.mp4").Length);
            Assert.AreEqual(0, Directory.GetFiles(_live, "seg_*.mp4").Length);
        }

        // ffmpegが無くても例外にせず理由を残す。録画が欠けても報告は成立させる設計のため
        // Missing ffmpeg records a reason instead of throwing, because a report stands without its recording
        [Test]
        public void ffmpegが無いときは警告を出して理由を残す()
        {
            LogAssert.Expect(LogType.Warning, $"録画リングを開始しません: {GameFrameRecorder.MissingFfmpegReason}");
            Assert.AreEqual(GameFrameRecorder.MissingFfmpegReason, GameFrameRecorder.ResolveUnavailableReason(null));
            Assert.AreEqual("", GameFrameRecorder.ResolveUnavailableReason("/opt/homebrew/bin/ffmpeg"));
        }

        private static void WriteSegment(string directory, string name, int minutesOffset)
        {
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, minutesOffset.ToString());
            File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(minutesOffset));
        }

        private static string ReadBody(string path)
        {
            return File.ReadAllText(path);
        }
    }
}
