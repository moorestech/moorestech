using System.IO;
using Game.Paths;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class WorldDataDirectoryTest
    {
        [Test]
        public void FromWorldRootDerivesAllPaths()
        {
            var dir = WorldDataDirectory.FromWorldRoot("/tmp/world_x");
            Assert.That(dir.WorldMetaFilePath, Is.EqualTo(Path.Combine("/tmp/world_x", "world.json")));
            Assert.That(dir.MapJsonFilePath, Is.EqualTo(Path.Combine("/tmp/world_x", "map.json")));
            Assert.That(dir.SaveJsonFilePath, Is.EqualTo(Path.Combine("/tmp/world_x", "save.json")));
            Assert.That(dir.TerrainDirectory, Is.EqualTo(Path.Combine("/tmp/world_x", "terrain")));
            Assert.That(dir.CacheDirectory, Is.EqualTo(Path.Combine("/tmp/world_x", "cache")));
            Assert.That(dir.CacheReadmeFilePath, Is.EqualTo(Path.Combine("/tmp/world_x", "cache", "README.txt")));
            Assert.That(dir.ProvisioningTempDirectory, Is.EqualTo("/tmp/world_x" + ".provisioning"));
        }

        [Test]
        public void FromServerDataMapUsesServerDataMapAndExplicitSave()
        {
            var dir = WorldDataDirectory.FromServerDataMap("/data/server_v8", "/tmp/save_test.json");
            Assert.That(dir.Root, Is.Null);
            Assert.That(dir.MapJsonFilePath, Is.EqualTo(Path.Combine("/data/server_v8", "map", "map.json")));
            Assert.That(dir.SaveJsonFilePath, Is.EqualTo("/tmp/save_test.json"));
        }
    }
}
