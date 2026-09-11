using System;
using System.IO;
using Client.Game.InGame.BugReport.Recording;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class VideoAssemblerTest
    {
        [Test]
        public void 生フレームから作った区間を結合しフレームを抜ける()
        {
            var ffmpeg = FfmpegLocator.Find();
            Assert.IsNotNull(ffmpeg, "ffmpeg が見つからない");
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-video-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);

            // 2秒ぶんの単色フレームを流して区間ファイルを作る
            // Feed two seconds of solid frames to produce segment files
            var recorder = FfmpegProcess.StartSegmentRecorder(ffmpeg, dir, 64, 36, 10, false);
            var frame = new byte[64 * 36 * 4];
            for (var i = 0; i < frame.Length; i += 4) { frame[i] = 200; frame[i + 3] = 255; }
            for (var i = 0; i < 20; i++) recorder.WriteFrame(frame);
            recorder.Stop();

            var segments = Directory.GetFiles(dir, "seg_*.mp4");
            Assert.GreaterOrEqual(segments.Length, 1);
            var output = Path.Combine(dir, "video.mp4");
            Assert.IsTrue(VideoAssembler.Concat(ffmpeg, segments, output));
            Assert.Greater(new FileInfo(output).Length, 0);

            var frames = Path.Combine(dir, "frames");
            Assert.IsTrue(VideoAssembler.ExtractFrames(ffmpeg, output, frames, 2));
            Assert.GreaterOrEqual(Directory.GetFiles(frames, "frame_*.jpg").Length, 3);

            // 2秒ぶんのフレームを流したので、本数×10秒固定の見積もりではなく実尺(約2秒)が返るはず
            // Fed two seconds of frames; the real (~2s) duration should come back, not the old count×10s estimate
            var duration = VideoAssembler.DurationSeconds(ffmpeg, output);
            Assert.Greater(duration, 0);
            Assert.Less(duration, GameFrameRecorder.SegmentSeconds, "固定10秒/区間の見積もりに戻っていないか");
            Directory.Delete(dir, true);
        }

        [Test]
        public void 区間が無ければ結合しない()
        {
            Assert.IsFalse(VideoAssembler.Concat("/nonexistent/ffmpeg", Array.Empty<string>(), "/tmp/never.mp4"));
        }
    }
}
