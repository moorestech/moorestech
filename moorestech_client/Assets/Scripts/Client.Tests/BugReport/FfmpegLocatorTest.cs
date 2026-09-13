using System;
using System.IO;
using Client.Game.InGame.BugReport.Recording;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class FfmpegLocatorTest
    {
        // ffmpegを積んでいない環境（CIコンテナ等）では検証対象そのものが無い。録画が要る開発機だけで実パスの実在を見る
        // Where ffmpeg is not installed (a CI container, say) there is nothing to verify; only a dev machine that needs recording checks the located path exists
        [Test]
        public void ffmpegのある環境では実在するパスを返す()
        {
            var path = FfmpegLocator.Find();
            if (path == null) Assert.Ignore("ffmpeg が無い環境のためスキップ（開発機では brew install ffmpeg）");
            Assert.IsTrue(File.Exists(path));
        }

        [Test]
        public void 環境変数の指定がPATHより優先される()
        {
            var fake = Path.Combine(Path.GetTempPath(), $"moorestech-ffmpeg-{Guid.NewGuid():N}");
            File.WriteAllText(fake, "");

            var found = FfmpegLocator.FindIn(fake, Path.GetDirectoryName(fake), new[] { "/opt/homebrew/bin/ffmpeg" });

            File.Delete(fake);
            Assert.AreEqual(fake, found);
        }

        [Test]
        public void PATH上のffmpegを拾う()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"moorestech-path-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var executable = Path.Combine(directory, "ffmpeg");
            File.WriteAllText(executable, "");

            var found = FfmpegLocator.FindIn("", directory, Array.Empty<string>());

            Directory.Delete(directory, true);
            Assert.AreEqual(executable, found);
        }

        // ffmpegが無い環境では null を返すだけで例外にしない（録画が欠けても報告は成立する設計）
        // Absent ffmpeg yields null instead of throwing, because a report still stands without its recording
        [Test]
        public void 見つからない環境ではnullを返す()
        {
            var empty = Path.Combine(Path.GetTempPath(), $"moorestech-nopath-{Guid.NewGuid():N}");
            Directory.CreateDirectory(empty);

            var found = FfmpegLocator.FindIn("", empty, Array.Empty<string>());

            Directory.Delete(empty, true);
            Assert.IsNull(found);
        }
    }
}
