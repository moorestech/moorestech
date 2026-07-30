using System;
using System.IO;
using Game.Paths;
using NUnit.Framework;

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
        }

        [Test]
        public void lowerHex16桁のワールドIDだけがキャッシュ直下へ解決される()
        {
            const string worldId = "0123456789abcdef";

            var actual = GameSystemPaths.GetWorldCacheDirectory(worldId);
            var expected = Path.Combine(GameSystemPaths.WorldCacheDirectory, worldId);

            Assert.That(Path.GetFullPath(actual), Is.EqualTo(Path.GetFullPath(expected)));
        }
    }
}
