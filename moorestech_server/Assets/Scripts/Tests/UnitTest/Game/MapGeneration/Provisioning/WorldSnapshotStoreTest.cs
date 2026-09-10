using System;
using System.IO;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Provisioning
{
    // 共有キャッシュへの書き戻しと、そこからの復元が往復することを検証する
    // Verifies the write-back into the shared cache and the restore out of it round-trip
    public class WorldSnapshotStoreTest
    {
        private string _worldId;
        private WorldDataDirectory _source;
        private WorldDataDirectory _restored;
        private string _serverDataDirectory;

        [SetUp]
        public void SetUp()
        {
            _worldId = Guid.NewGuid().ToString("N").Substring(0, GameSystemPaths.WorldIdHexDigits);
            _source = WorldDataDirectory.FromWorldRoot(Path.Combine(Path.GetTempPath(), "WorldSnapshotStoreTest_src_" + _worldId));
            _restored = WorldDataDirectory.FromWorldRoot(Path.Combine(Path.GetTempPath(), "WorldSnapshotStoreTest_dst_" + _worldId));
            _serverDataDirectory = Path.Combine(Path.GetTempPath(), "WorldSnapshotStoreTest_server_" + _worldId);
        }

        [TearDown]
        public void TearDown()
        {
            var sharedCache = WorldDataDirectory.ForWorldCache(_worldId);
            foreach (var root in new[] { _source.Root, _restored.Root, _restored.ProvisioningTempDirectory, sharedCache.Root, sharedCache.ProvisioningTempDirectory, _serverDataDirectory })
                if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void 同梱スナップショットは一時ディレクトリ経由で共有キャッシュへ確定し中断の残骸も残らない()
        {
            var bundled = WorldDataDirectory.ForBundledSnapshot(_serverDataDirectory, _worldId);
            Directory.CreateDirectory(bundled.TerrainDirectory);
            Directory.CreateDirectory(bundled.TerrainVisualDirectory);
            File.WriteAllText(bundled.MapJsonFilePath, "{\"map\":2}");
            File.WriteAllBytes(bundled.TerrainHeightFilePath(0, 0), new byte[] { 4, 5 });
            File.WriteAllBytes(bundled.TerrainVisualCacheFilePath(0, 0), new byte[] { 6 });
            File.WriteAllText(bundled.WorldMetaFilePath, "{\"seed\":1,\"mapMode\":\"generated\",\"createdAt\":\"2000-01-01T00:00:00.0000000Z\"}");

            // 前回中断の残骸(共有キャッシュ側・本番側の一時ディレクトリ)を置いた状態から復元する
            // Restore starting from leftovers of an earlier interruption in both the cache-side and world-side temp dirs
            var sharedCache = WorldDataDirectory.ForWorldCache(_worldId);
            Directory.CreateDirectory(sharedCache.ProvisioningTempDirectory);
            File.WriteAllText(Path.Combine(sharedCache.ProvisioningTempDirectory, "stale.txt"), "x");
            Directory.CreateDirectory(_restored.ProvisioningTempDirectory);

            Assert.IsTrue(WorldSnapshotStore.TryRestore(_restored, _serverDataDirectory, _worldId));

            Assert.IsTrue(WorldSnapshotStore.IsSnapshot(sharedCache));
            Assert.AreEqual(new byte[] { 6 }, File.ReadAllBytes(sharedCache.TerrainVisualCacheFilePath(0, 0)), "同梱のvisualも共有キャッシュへ写る");
            Assert.IsFalse(File.Exists(Path.Combine(sharedCache.Root, "stale.txt")), "残骸は確定先へ持ち込まれない");
            Assert.IsFalse(Directory.Exists(sharedCache.ProvisioningTempDirectory));
            Assert.IsFalse(Directory.Exists(_restored.ProvisioningTempDirectory));
            Assert.AreEqual(new byte[] { 4, 5 }, File.ReadAllBytes(_restored.TerrainHeightFilePath(0, 0)));
        }

        [Test]
        public void 共有キャッシュに本体だけある状態でも同梱源のvisualが取り込まれる()
        {
            var bundled = WorldDataDirectory.ForBundledSnapshot(_serverDataDirectory, _worldId);
            Directory.CreateDirectory(bundled.TerrainDirectory);
            Directory.CreateDirectory(bundled.TerrainVisualDirectory);
            File.WriteAllText(bundled.MapJsonFilePath, "{\"map\":2}");
            File.WriteAllBytes(bundled.TerrainHeightFilePath(0, 0), new byte[] { 4, 5 });
            File.WriteAllBytes(bundled.TerrainVisualCacheFilePath(0, 0), new byte[] { 6 });
            File.WriteAllText(bundled.WorldMetaFilePath, "{\"seed\":1,\"mapMode\":\"generated\",\"createdAt\":\"2000-01-01T00:00:00.0000000Z\"}");

            // 共有キャッシュはvisualの無い本体だけ。ここで同梱源を無視すると先焼きが復活する
            // The shared cache holds only the core with no visuals; ignoring the bundled source here brings the prebake back
            Directory.CreateDirectory(_source.TerrainDirectory);
            File.WriteAllText(_source.MapJsonFilePath, "{\"map\":1}");
            File.WriteAllText(_source.WorldMetaFilePath, "{\"seed\":1,\"mapMode\":\"generated\",\"createdAt\":\"2000-01-01T00:00:00.0000000Z\"}");
            WorldSnapshotStore.Store(_source, _worldId);

            Assert.IsTrue(WorldSnapshotStore.TryRestore(_restored, _serverDataDirectory, _worldId));

            var sharedCache = WorldDataDirectory.ForWorldCache(_worldId);
            Assert.AreEqual(new byte[] { 6 }, File.ReadAllBytes(sharedCache.TerrainVisualCacheFilePath(0, 0)));
            Assert.AreEqual("{\"map\":2}", File.ReadAllText(sharedCache.MapJsonFilePath), "共有キャッシュは同梱源の完全体で置き換わる");
        }

        [Test]
        public void 本番Rootは一時ディレクトリから一括で確定され残骸を残さない()
        {
            Directory.CreateDirectory(_source.TerrainDirectory);
            File.WriteAllText(_source.MapJsonFilePath, "{\"map\":1}");
            File.WriteAllText(_source.WorldMetaFilePath, "{\"seed\":1,\"mapMode\":\"generated\",\"createdAt\":\"2000-01-01T00:00:00.0000000Z\"}");
            WorldSnapshotStore.Store(_source, _worldId);

            Assert.IsFalse(Directory.Exists(_restored.Root));
            Assert.IsTrue(WorldSnapshotStore.TryRestore(_restored, _serverDataDirectory, _worldId));

            // 確定後は本番Rootだけが残り、createdAtは復元時刻に置き換わっている
            // After the commit only the production root remains, with createdAt replaced by the restore time
            Assert.IsTrue(File.Exists(_restored.WorldMetaFilePath));
            Assert.IsFalse(Directory.Exists(_restored.ProvisioningTempDirectory));
            Assert.IsFalse(File.ReadAllText(_restored.WorldMetaFilePath).Contains("2000-01-01"));
        }

        [Test]
        public void Storeした世界はTryRestoreで同じ内容に復元されcreatedAtだけ復元時刻になる()
        {
            Directory.CreateDirectory(_source.TerrainDirectory);
            File.WriteAllText(_source.MapJsonFilePath, "{\"map\":1}");
            File.WriteAllBytes(_source.TerrainHeightFilePath(0, 0), new byte[] { 1, 2, 3 });
            File.WriteAllText(_source.WorldMetaFilePath, "{\"seed\":196,\"mapMode\":\"generated\",\"createdAt\":\"2000-01-01T00:00:00.0000000Z\"}");

            WorldSnapshotStore.Store(_source, _worldId);
            var restored = WorldSnapshotStore.TryRestore(_restored, Path.GetTempPath(), _worldId);

            Assert.IsTrue(restored);
            Assert.AreEqual("{\"map\":1}", File.ReadAllText(_restored.MapJsonFilePath));
            Assert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(_restored.TerrainHeightFilePath(0, 0)));
            Assert.IsFalse(File.ReadAllText(_restored.WorldMetaFilePath).Contains("2000-01-01"));
            Assert.IsTrue(File.ReadAllText(_restored.WorldMetaFilePath).Contains("\"seed\": 196"));
        }

        [Test]
        public void スナップショットが無ければTryRestoreはfalseで何も作らない()
        {
            Assert.IsFalse(WorldSnapshotStore.TryRestore(_restored, Path.GetTempPath(), _worldId));
            Assert.IsFalse(File.Exists(_restored.WorldMetaFilePath));
        }

        [Test]
        public void 生成入力が同じならworldIdは同じで入力が1つでも違えば別になる()
        {
            var a = WorldIdentity.CalculateGenerated(196, "fp", "4.0.0");
            Assert.AreEqual(a, WorldIdentity.CalculateGenerated(196, "fp", "4.0.0"));
            Assert.AreEqual(GameSystemPaths.WorldIdHexDigits, a.Length);
            Assert.AreNotEqual(a, WorldIdentity.CalculateGenerated(197, "fp", "4.0.0"));
            Assert.AreNotEqual(a, WorldIdentity.CalculateGenerated(196, "fp2", "4.0.0"));
            Assert.AreNotEqual(a, WorldIdentity.CalculateGenerated(196, "fp", "4.0.1"));
        }
    }
}
