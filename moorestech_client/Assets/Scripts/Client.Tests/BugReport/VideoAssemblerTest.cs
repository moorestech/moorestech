using System;
using System.IO;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport.Recording;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            var framePool = new FrameBufferPool(2, 64 * 36 * 4);
            var recorder = FfmpegProcess.StartSegmentRecorder(ffmpeg, dir, 64, 36, 10, false, framePool);
            for (var i = 0; i < 20; i++)
            {
                // バッファは書き終えた時点でプールへ返るので、借り直しながら流す
                // Buffers come back to the pool once written, so each frame borrows one again
                Assert.IsTrue(framePool.TryRent(out var frame), "書き込みが追いつかずバッファが返ってこない");
                for (var pixel = 0; pixel < frame.Length; pixel += 4) { frame[pixel] = 200; frame[pixel + 3] = 255; }
                recorder.WriteFrame(frame);
            }
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
            Assert.IsTrue(VideoAssembler.TryDurationSeconds(ffmpeg, output, out var duration));
            Assert.Greater(duration, 0);
            Assert.Less(duration, GameFrameRecorder.SegmentSeconds, "固定10秒/区間の見積もりに戻っていないか");
            Directory.Delete(dir, true);
        }

        [Test]
        public void 区間が無ければ結合しない()
        {
            Assert.IsFalse(VideoAssembler.Concat("/nonexistent/ffmpeg", Array.Empty<string>(), "/tmp/never.mp4"));
        }

        // 尺を測れなかったときに0を返すと「0秒の動画」という実値がmanifestへ焼かれる
        // Returning 0 for an unmeasurable duration bakes "a zero-second video" into the manifest as a real value
        [Test]
        public void 尺を測れなければ実値を返さない()
        {
            LogAssert.Expect(LogType.Error, new Regex("failed to start"));
            LogAssert.Expect(LogType.Warning, new Regex("尺を測るffmpegを起動できませんでした"));
            Assert.IsFalse(VideoAssembler.TryDurationSeconds("/nonexistent/ffmpeg", "/tmp/never.mp4", out var duration));
            Assert.AreEqual(0, duration);
        }
    }
}
