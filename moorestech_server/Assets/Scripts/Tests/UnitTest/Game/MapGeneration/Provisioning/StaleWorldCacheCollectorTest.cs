using System;
using System.IO;
using Game.MapGeneration.Provisioning;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Provisioning
{
    // 現在のワールドだけを残す掃除の挙動を、実ユーザーのキャッシュを触らない一時ルート上で検証する
    // Verifies that the collector keeps only the current world, on a temp root that never touches the real user cache
    public class StaleWorldCacheCollectorTest
    {
        private string _cacheRoot;

        [SetUp]
        public void SetUp()
        {
            _cacheRoot = Path.Combine(Path.GetTempPath(), "StaleWorldCacheCollectorTest_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_cacheRoot)) Directory.Delete(_cacheRoot, true);
        }

        [Test]
        public void 現在のワールドキャッシュは残り別IDのキャッシュだけが消える()
        {
            var current = Path.Combine(_cacheRoot, "0123456789abcdef");
            var stale = Path.Combine(_cacheRoot, "fedcba9876543210");
            Directory.CreateDirectory(current);
            Directory.CreateDirectory(Path.Combine(stale, "terrain"));
            File.WriteAllText(Path.Combine(stale, "terrain", "height_0_0.bin"), "x");

            StaleWorldCacheCollector.Collect(_cacheRoot, "0123456789abcdef");

            Assert.IsTrue(Directory.Exists(current));
            Assert.IsFalse(Directory.Exists(stale));
        }

        [Test]
        public void キャッシュルートが無ければ何もしない()
        {
            Assert.DoesNotThrow(() => StaleWorldCacheCollector.Collect(_cacheRoot, "0123456789abcdef"));
            Assert.IsFalse(Directory.Exists(_cacheRoot));
        }
    }
}
