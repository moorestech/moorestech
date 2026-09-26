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

        // 配布物同梱の実行ファイルが、環境変数指定より優先して見つかる
        // The bundled executable is found ahead of the env-var override
        [Test]
        public void 同梱パスが環境変数より優先される()
        {
            var bundled = Path.Combine(Path.GetTempPath(), $"moorestech-bundled-{Guid.NewGuid():N}");
            var overridePath = Path.Combine(Path.GetTempPath(), $"moorestech-override-{Guid.NewGuid():N}");
            File.WriteAllText(bundled, "");
            File.WriteAllText(overridePath, "");

            var found = FfmpegLocator.FindIn(bundled, overridePath, Path.GetDirectoryName(overridePath), "ffmpeg", new[] { "/opt/homebrew/bin/ffmpeg" });

            File.Delete(bundled);
            File.Delete(overridePath);
            Assert.AreEqual(bundled, found);
        }

        [Test]
        public void 環境変数の指定がPATHより優先される()
        {
            var fake = Path.Combine(Path.GetTempPath(), $"moorestech-ffmpeg-{Guid.NewGuid():N}");
            File.WriteAllText(fake, "");

            var found = FfmpegLocator.FindIn("", fake, Path.GetDirectoryName(fake), "ffmpeg", new[] { "/opt/homebrew/bin/ffmpeg" });

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

            var found = FfmpegLocator.FindIn("", "", directory, "ffmpeg", Array.Empty<string>());

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

            var found = FfmpegLocator.FindIn("", "", empty, "ffmpeg", Array.Empty<string>());

            Directory.Delete(empty, true);
            Assert.IsNull(found);
        }

        // Mac Playerは.app/Contents/MacOS/ffmpegを同梱位置として探す
        // A Mac player looks for the bundled copy at .app/Contents/MacOS/ffmpeg
        [Test]
        public void MacPlayerの同梱位置はContents配下のMacOS()
        {
            var dataPath = Path.Combine("moorestech.app", "Contents");
            var path = FfmpegLocator.ResolveBundledPath(dataPath, UnityEngine.RuntimePlatform.OSXPlayer);
            Assert.AreEqual(Path.Combine(dataPath, "MacOS", "ffmpeg"), path);
        }

        [Test]
        public void WindowsPlayerの同梱位置はPluginsのx86_64()
        {
            var dataPath = "moorestech_Data";
            var path = FfmpegLocator.ResolveBundledPath(dataPath, UnityEngine.RuntimePlatform.WindowsPlayer);
            Assert.AreEqual(Path.Combine(dataPath, "Plugins", "x86_64", "ffmpeg.exe"), path);
        }

        // Editorには同梱物が無いので同梱位置を持たない
        // The Editor has no bundled copy, so it has no bundled location
        [Test]
        public void Editorには同梱位置が無い()
        {
            Assert.AreEqual(string.Empty, FfmpegLocator.ResolveBundledPath("Assets", UnityEngine.RuntimePlatform.OSXEditor));
        }
    }
}
