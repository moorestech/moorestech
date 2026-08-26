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

        [SetUp]
        public void SetUp()
        {
            _worldId = Guid.NewGuid().ToString("N").Substring(0, WorldIdentity.HexDigits);
            _source = WorldDataDirectory.FromWorldRoot(Path.Combine(Path.GetTempPath(), "WorldSnapshotStoreTest_src_" + _worldId));
            _restored = WorldDataDirectory.FromWorldRoot(Path.Combine(Path.GetTempPath(), "WorldSnapshotStoreTest_dst_" + _worldId));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in new[] { _source.Root, _restored.Root, WorldDataDirectory.ForWorldCache(_worldId).Root })
                if (Directory.Exists(root)) Directory.Delete(root, true);
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
            Assert.AreEqual(WorldIdentity.HexDigits, a.Length);
            Assert.AreNotEqual(a, WorldIdentity.CalculateGenerated(197, "fp", "4.0.0"));
            Assert.AreNotEqual(a, WorldIdentity.CalculateGenerated(196, "fp2", "4.0.0"));
            Assert.AreNotEqual(a, WorldIdentity.CalculateGenerated(196, "fp", "4.0.1"));
        }
    }
}
