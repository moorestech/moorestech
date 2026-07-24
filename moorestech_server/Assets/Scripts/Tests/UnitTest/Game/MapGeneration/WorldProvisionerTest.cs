using System;
using System.Diagnostics;
using System.IO;
using Core.Master;
using Game.Map.Interface.Json;
using Game.MapGeneration.Provisioning;
using Game.Paths;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Debug = UnityEngine.Debug;

namespace Tests.UnitTest.Game.MapGeneration
{
    // WorldProvisioner.EnsureWorldのアトミック確定・破損検出・no-op挙動を検証する
    // Verifies WorldProvisioner.EnsureWorld's atomic commit, corruption detection, and no-op behavior
    public class WorldProvisionerTest
    {
        private string _worldRoot;

        [SetUp]
        public void SetUp()
        {
            _worldRoot = Path.Combine(Path.GetTempPath(), "WorldProvisionerTest_" + Guid.NewGuid());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_worldRoot)) Directory.Delete(_worldRoot, true);
            var tempDir = _worldRoot + ".provisioning";
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        [Test]
        public void TemplateModeで新規作成するとworld_jsonとmap_jsonが元と同一内容で作られる()
        {
            var dir = WorldDataDirectory.FromWorldRoot(_worldRoot);
            var settings = new WorldProvisionSettings(dir, TestModDirectory.ForUnitTestModDirectory, "template", 0);

            WorldProvisioner.EnsureWorld(settings);

            Assert.IsTrue(File.Exists(dir.WorldMetaFilePath));
            var sourcePath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "map", "map.json");
            Assert.AreEqual(File.ReadAllText(sourcePath), File.ReadAllText(dir.MapJsonFilePath));
        }

        [Test]
        public void GeneratedModeで新規作成するとmap_jsonがMapInfoJsonとしてデシリアライズできterrainファイルが存在する()
        {
            LoadMasterHolderForGeneration();

            var dir = WorldDataDirectory.FromWorldRoot(_worldRoot);
            var settings = new WorldProvisionSettings(dir, TestModDirectory.ForUnitTestModDirectory, "generated", 12345);

            // 生成時間を計測して記録する(仕様書のリスク欄へ反映するため)
            // Measure generation time to record it (feeds the spec's risk section)
            var stopwatch = Stopwatch.StartNew();
            WorldProvisioner.EnsureWorld(settings);
            stopwatch.Stop();
            Debug.Log($"[WorldProvisionerTest] generated mode EnsureWorld elapsed={stopwatch.ElapsedMilliseconds}ms");

            var mapInfoJson = JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(dir.MapJsonFilePath));
            Assert.IsNotNull(mapInfoJson);
            Assert.IsTrue(File.Exists(dir.WorldMetaFilePath));
            Assert.IsTrue(File.Exists(Path.Combine(dir.TerrainDirectory, "height_0_0.r16")));
            Assert.IsTrue(File.Exists(Path.Combine(dir.TerrainDirectory, "biome_0_0.bin")));
        }

        [Test]
        public void 二回目の呼び出しはno_opでファイルのタイムスタンプが変わらない()
        {
            var dir = WorldDataDirectory.FromWorldRoot(_worldRoot);
            var settings = new WorldProvisionSettings(dir, TestModDirectory.ForUnitTestModDirectory, "template", 0);

            WorldProvisioner.EnsureWorld(settings);
            var firstWriteTime = File.GetLastWriteTimeUtc(dir.WorldMetaFilePath);

            WorldProvisioner.EnsureWorld(settings);
            var secondWriteTime = File.GetLastWriteTimeUtc(dir.WorldMetaFilePath);

            Assert.AreEqual(firstWriteTime, secondWriteTime);
        }

        [Test]
        public void provisioning残骸がある状態で呼ぶと残骸が消えて正常に生成される()
        {
            var dir = WorldDataDirectory.FromWorldRoot(_worldRoot);
            Directory.CreateDirectory(dir.ProvisioningTempDirectory);
            File.WriteAllText(Path.Combine(dir.ProvisioningTempDirectory, "leftover.txt"), "stale");

            var settings = new WorldProvisionSettings(dir, TestModDirectory.ForUnitTestModDirectory, "template", 0);
            WorldProvisioner.EnsureWorld(settings);

            Assert.IsFalse(Directory.Exists(dir.ProvisioningTempDirectory));
            Assert.IsTrue(File.Exists(dir.WorldMetaFilePath));
        }

        [Test]
        public void Rootは存在するがworld_jsonが無い場合は破損として例外を投げる()
        {
            var dir = WorldDataDirectory.FromWorldRoot(_worldRoot);
            Directory.CreateDirectory(dir.Root);
            File.WriteAllText(dir.MapJsonFilePath, "{}");

            var settings = new WorldProvisionSettings(dir, TestModDirectory.ForUnitTestModDirectory, "template", 0);

            Assert.Throws<InvalidOperationException>(() => WorldProvisioner.EnsureWorld(settings));
        }

        #region Internal

        private static void LoadMasterHolderForGeneration()
        {
            // generated modeはMasterHolder.GenerationMaster.SelectedGenerationを要求するため、
            // ForUnitTest modをDIコンテナ生成経由でロードしておく
            // generated mode requires MasterHolder.GenerationMaster.SelectedGeneration,
            // so load the ForUnitTest mod via DI container generation
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        #endregion
    }
}
