using System;
using System.IO;
using Client.Game.InGame.BugReport.Recording;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class FfmpegLocatorTest
    {
        [Test]
        public void この開発機ではffmpegが見つかる()
        {
            var path = FfmpegLocator.Find();
            Assert.IsNotNull(path, "ffmpeg が見つからない（brew install ffmpeg）");
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
