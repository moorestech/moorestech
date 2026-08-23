using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Core.Master;
using Game.Map.Interface.Json;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Mooresmaster.Model.MapModule;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Tests.UnitTest.Game.MapGeneration
{
    // WorldProvisioner.EnsureWorldのアトミック確定・破損検出・no-op挙動を検証する
    // Verifies WorldProvisioner.EnsureWorld's atomic commit, corruption detection, and no-op behavior
    public class WorldProvisionerTest
    {
        private WorldDataDirectory _worldDataDirectory;

        [SetUp]
        public void SetUp()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), "WorldProvisionerTest_" + Guid.NewGuid());
            _worldDataDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);
        }

        [TearDown]
        public void TearDown()
        {
            // 削除対象のパスはWorldDataDirectoryから取る。テスト側でパス規則を再導出しない
            // Take the paths to delete from WorldDataDirectory; never re-derive the path rules here
            if (Directory.Exists(_worldDataDirectory.Root)) Directory.Delete(_worldDataDirectory.Root, true);
            if (Directory.Exists(_worldDataDirectory.ProvisioningTempDirectory)) Directory.Delete(_worldDataDirectory.ProvisioningTempDirectory, true);
        }

        [Test]
        public void TemplateModeで新規作成するとworld_jsonとmap_jsonが元と同一内容で作られる()
        {
            var settings = new WorldProvisionSettings(_worldDataDirectory, TestModDirectory.ForUnitTestModDirectory, "template", 0);

            WorldProvisioner.EnsureWorld(settings);

            Assert.IsTrue(File.Exists(_worldDataDirectory.WorldMetaFilePath));
            var sourcePath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "map", "map.json");
            Assert.AreEqual(File.ReadAllText(sourcePath), File.ReadAllText(_worldDataDirectory.MapJsonFilePath));
        }

        [Test]
        public void GeneratedModeで新規作成するとmap_jsonがMapInfoJsonとしてデシリアライズできterrainファイルが存在する()
        {
            LoadMasterHolderForGeneration();

            var settings = new WorldProvisionSettings(_worldDataDirectory, TestModDirectory.ForUnitTestModDirectory, "generated", 12345);

            // 生成時間を計測して記録する(仕様書のリスク欄へ反映するため)
            // Measure generation time to record it (feeds the spec's risk section)
            var stopwatch = Stopwatch.StartNew();
            WorldProvisioner.EnsureWorld(settings);
            stopwatch.Stop();
            Debug.Log($"[WorldProvisionerTest] generated mode EnsureWorld elapsed={stopwatch.ElapsedMilliseconds}ms");

            var mapInfoJson = JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(_worldDataDirectory.MapJsonFilePath));
            Assert.IsNotNull(mapInfoJson);
            Assert.IsTrue(File.Exists(_worldDataDirectory.WorldMetaFilePath));
            Assert.IsTrue(File.Exists(_worldDataDirectory.TerrainHeightFilePath(0, 0)));

            // mapVeinsをveinGuid→MapVeinMasterのveinTypeで振り分け、item/fluid双方の非空を検証する
            // Classify mapVeins by veinType via veinGuid→MapVeinMaster lookup; verify both are non-empty
            var (itemVeinPositions, fluidVeinPositions) = ClassifyVeinsByType();
            Assert.That(fluidVeinPositions, Is.Not.Empty, "generated map.json should contain at least one veinType=fluid vein");
            Assert.That(itemVeinPositions, Is.Not.Empty, "generated map.json should contain at least one veinType=item vein");

            // rngSeedOffset分離(VeinPlacementCore)の狙い通りitem/fluidが同一座標に重ならないことを固定する
            // Pin that the rngSeedOffset separation (VeinPlacementCore) keeps item/fluid veins off identical positions
            Assert.IsFalse(itemVeinPositions.ToHashSet().Overlaps(fluidVeinPositions), "item and fluid veins should not collapse onto identical positions");

            #region Internal

            (List<Vector3Int> itemPositions, List<Vector3Int> fluidPositions) ClassifyVeinsByType()
            {
                var itemPositions = new List<Vector3Int>();
                var fluidPositions = new List<Vector3Int>();
                foreach (var vein in mapInfoJson.MapVeins)
                {
                    var element = MasterHolder.MapVeinMaster.GetElementOrNull(vein.VeinGuid);
                    Assert.IsNotNull(element, $"veinGuid {vein.VeinGuid} was not found in MapVeinMaster");
                    if (element.VeinParam is FluidVeinParam) fluidPositions.Add(vein.MinPosition);
                    else if (element.VeinParam is ItemVeinParam) itemPositions.Add(vein.MinPosition);
                }
                return (itemPositions, fluidPositions);
            }

            #endregion
        }

        // 定数同士の比較はどんな版でも通るトートロジーになるため、版そのものをリテラルで固定する
        // Comparing the constant to itself is a tautology regardless of value, so pin the version as a literal
        [Test]
        public void GeneratorVersion定数は3_0_0に固定されている()
        {
            Assert.AreEqual("3.0.0", WorldProvisioner.GeneratorVersion);
        }

        [Test]
        public void 生成ワールドはグリッド積のタイル数と版定数を記録し全タイルのファイルを書き出す()
        {
            LoadMasterHolderForGeneration();

            var settings = new WorldProvisionSettings(_worldDataDirectory, TestModDirectory.ForUnitTestModDirectory, "generated", 12345);
            WorldProvisioner.EnsureWorld(settings);

            var worldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(_worldDataDirectory.WorldMetaFilePath));
            var config = GenerationRuntimeConfigFactory.Build(MasterHolder.GenerationMaster.SelectedGeneration);
            Assert.AreEqual(config.gridSizeX * config.gridSizeZ, worldMeta.TerrainTileCount);
            Assert.AreEqual(WorldProvisioner.GeneratorVersion, worldMeta.GeneratorVersion);

            // 全タイルのファイルが存在する / every tile's files exist
            foreach (var (tileX, tileZ) in TerrainTransferMeta.EnumerateTileCoordinates(worldMeta.TerrainTileCount))
                Assert.IsTrue(File.Exists(_worldDataDirectory.TerrainHeightFilePath(tileX, tileZ)));
        }

        [Test]
        public void 二回目の呼び出しはno_opでファイルのタイムスタンプが変わらない()
        {
            var settings = new WorldProvisionSettings(_worldDataDirectory, TestModDirectory.ForUnitTestModDirectory, "template", 0);

            WorldProvisioner.EnsureWorld(settings);
            var firstWriteTime = File.GetLastWriteTimeUtc(_worldDataDirectory.WorldMetaFilePath);

            WorldProvisioner.EnsureWorld(settings);
            var secondWriteTime = File.GetLastWriteTimeUtc(_worldDataDirectory.WorldMetaFilePath);

            Assert.AreEqual(firstWriteTime, secondWriteTime);
        }

        [Test]
        public void provisioning残骸がある状態で呼ぶと残骸が消えて正常に生成される()
        {
            Directory.CreateDirectory(_worldDataDirectory.ProvisioningTempDirectory);
            File.WriteAllText(Path.Combine(_worldDataDirectory.ProvisioningTempDirectory, "leftover.txt"), "stale");

            var settings = new WorldProvisionSettings(_worldDataDirectory, TestModDirectory.ForUnitTestModDirectory, "template", 0);
            WorldProvisioner.EnsureWorld(settings);

            Assert.IsFalse(Directory.Exists(_worldDataDirectory.ProvisioningTempDirectory));
            Assert.IsTrue(File.Exists(_worldDataDirectory.WorldMetaFilePath));
        }

        [Test]
        public void Rootは存在するがworld_jsonが無い場合は破損として例外を投げる()
        {
            Directory.CreateDirectory(_worldDataDirectory.Root);
            File.WriteAllText(_worldDataDirectory.MapJsonFilePath, "{}");

            var settings = new WorldProvisionSettings(_worldDataDirectory, TestModDirectory.ForUnitTestModDirectory, "template", 0);

            Assert.Throws<InvalidOperationException>(() => WorldProvisioner.EnsureWorld(settings));
        }

        private static void LoadMasterHolderForGeneration()
        {
            // generated modeはMasterHolder.GenerationMaster.SelectedGenerationを要求するため、
            // ForUnitTest modをDIコンテナ生成経由でロードしておく
            // generated mode requires MasterHolder.GenerationMaster.SelectedGeneration,
            // so load the ForUnitTest mod via DI container generation
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
