using System;
using System.IO;
using Game.Paths;
using NUnit.Framework;
using Server.Boot;

namespace Tests.UnitTest.Game.Paths
{
    public class GameSystemPathsTest
    {
        [TestCase("")]
        [TestCase(null)]
        [TestCase("../outside")]
        [TestCase("0123456789ABCDEf")]
        [TestCase("0123456789abcdef0")]
        [TestCase("/tmp/0123456789abcdef")]
        public void ワールドキャッシュは生成規則外のIDを拒否する(string worldId)
        {
            Assert.Throws<ArgumentException>(() => GameSystemPaths.GetWorldCacheDirectory(worldId));
            Assert.Throws<ArgumentException>(() => GameSystemPaths.GetWorldCacheDirectoryPathWithoutCreating(worldId));
        }

        [Test]
        public void 作らない版は作る版と同じパスを返しディレクトリを作らない()
        {
            // 実キャッシュに残らないよう、既存と衝突しないランダムなIDを使う
            // A random id avoids colliding with real cache entries and leaves nothing behind
            var worldId = Guid.NewGuid().ToString("N").Substring(0, GameSystemPaths.WorldIdHexDigits);

            var actual = GameSystemPaths.GetWorldCacheDirectoryPathWithoutCreating(worldId);
            var expected = Path.Combine(GameSystemPaths.WorldCacheDirectory, worldId);

            Assert.That(Path.GetFullPath(actual), Is.EqualTo(Path.GetFullPath(expected)));
            Assert.IsFalse(Directory.Exists(actual));
        }

        [Test]
        public void lowerHex16桁のワールドIDだけがキャッシュ直下へ解決される()
        {
            const string worldId = "0123456789abcdef";

            var actual = GameSystemPaths.GetWorldCacheDirectory(worldId);
            var expected = Path.Combine(GameSystemPaths.WorldCacheDirectory, worldId);

            Assert.That(Path.GetFullPath(actual), Is.EqualTo(Path.GetFullPath(expected)));
        }

        // 上書きキーは内蔵サーバーの既定ワールドと既定ワールドの削除の両方を一時ディレクトリへ向け、Saves/world_1に触れない
        // The override key points both the embedded server's default world and the default-world deletion at a temporary directory, leaving Saves/world_1 untouched
        [Test]
        public void 上書きキーで既定ワールドの読み書きと削除が一時ディレクトリへ向く()
        {
            var previousOverride = Environment.GetEnvironmentVariable(GameSystemPaths.DefaultWorldDirectoryOverrideEnvKey);
            var temporaryWorldDirectory = Path.Combine(Path.GetTempPath(), $"moorestech_default_world_override_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(temporaryWorldDirectory);
            Environment.SetEnvironmentVariable(GameSystemPaths.DefaultWorldDirectoryOverrideEnvKey, temporaryWorldDirectory);

            // 失敗しても上書きを戻す。残すと後続テストの既定ワールドがずれる
            // Restore the override even on failure; leaving it would shift later tests' default world
            try
            {
                Assert.AreEqual(temporaryWorldDirectory, GameSystemPaths.DefaultWorldDirectory);
                Assert.AreEqual(temporaryWorldDirectory, new StartServerSettings().WorldDirectory);
                Assert.IsTrue(GameSystemPaths.DeleteDefaultWorldDirectory());
                Assert.IsFalse(Directory.Exists(temporaryWorldDirectory));
            }
            finally
            {
                Environment.SetEnvironmentVariable(GameSystemPaths.DefaultWorldDirectoryOverrideEnvKey, previousOverride);
                if (Directory.Exists(temporaryWorldDirectory)) Directory.Delete(temporaryWorldDirectory, true);
            }
        }
    }
}
