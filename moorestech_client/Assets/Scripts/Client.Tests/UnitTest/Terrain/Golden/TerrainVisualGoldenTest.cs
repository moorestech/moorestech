using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.Environment.Terrain.Build;
using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain.Golden
{
    /// <summary>
    ///     移設前の見た目（alphamap・detail密度・表示用高さ）のSHA256を固定する。移設の各タスクはこのテストが通ることを完了条件にする
    ///     Pins the pre-migration visuals (alphamap, detail density, display heights) as SHA256; every migration task must keep it green
    /// </summary>
    public class TerrainVisualGoldenTest
    {
        [SetUp]
        public void SetUp()
        {
            // VanillaGeneratorの配置段がMasterHolder経由でMapObjectMasterを引く。ロードしないとNREで落ちる
            // VanillaGenerator's placement stages resolve MapObjectMaster through MasterHolder; without loading it this throws an NRE
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void VisualsMatchGolden()
        {
            var (config, sections, output) = TerrainVisualGoldenFixture.Build();
            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech_golden_{System.Guid.NewGuid()}");
            var worldDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);
            var actual = new Dictionary<string, string>();

            // finallyで一時ディレクトリを必ず片付ける。途中でResolveが例外を投げても取り残さない
            // The finally block always cleans the temp directory, even if Resolve throws partway through
            try
            {
                TerrainFileWriter.Write(worldDirectory, output);

                // 台帳は生成パイプラインが既に組んでいる。ワイヤへ往復させる必要はない
                // The ledger is already assembled by the generation pipeline; there is no need to round-trip it over the wire
                var placements = output.Ledger.Placements;

                var gridConfig = config.ShallowCopy();
                gridConfig.worldOffsetX = output.NoiseOrigin.x;
                gridConfig.worldOffsetZ = output.NoiseOrigin.y;
                var helper = new BiomePlacementHelper(gridConfig);
                var species = TreeSurroundSpeciesTable.Build(helper, TerrainVisualGoldenFixture.BiomeTypes);
                var layerTable = SplatLayerTable.Build("addr/beach", "addr/rock", sections.MainLayerAddresses, sections.TextureConfigs,
                    sections.SurroundTextureConfigs, species, System.Array.Empty<string>());
                var provider = new TerrainTileVisualProvider(gridConfig, TerrainVisualGoldenFixture.BiomeTypes, sections, layerTable,
                    new TerrainLayer[layerTable.OrderedLayerAddresses.Count], species, placements, worldDirectory,
                    new TerrainVisualCache(worldDirectory, new string('0', 64)));

                foreach (var (tileX, tileZ) in TerrainTransferMeta.EnumerateTileCoordinates(output.Tiles.Count))
                {
                    var tileConfig = gridConfig.CreateTileConfig(tileX, tileZ);
                    var tileScene = config.TileScenePosition(tileX, tileZ);
                    var tileWorld = new Vector3(tileScene.x, 0f, tileScene.y);
                    var pre = HeightFileLoader.LoadHeights(worldDirectory, tileX, tileZ, config.Resolution);
                    var post = TreePerturbationApplier.Apply(pre, tileConfig, tileWorld, placements);
                    var (visual, _) = provider.Resolve(tileX, tileZ, tileConfig, tileWorld, pre, post);
                    actual[$"alphamap_{tileX}_{tileZ}"] = TerrainVisualGoldenFixture.Sha256(visual.Alphamap);
                    actual[$"heights_{tileX}_{tileZ}"] = TerrainVisualGoldenFixture.Sha256(post);
                    for (var d = 0; d < visual.DetailMaps.Count; d++)
                        actual[$"detail_{tileX}_{tileZ}_{d}"] = TerrainVisualGoldenFixture.Sha256(visual.DetailMaps[d]);
                }
            }
            finally
            {
                Directory.Delete(worldRoot, true);
            }

            var goldenPath = TerrainVisualGoldenFixture.GoldenJsonPath;
            if (!File.Exists(goldenPath))
            {
                File.WriteAllText(goldenPath, JsonConvert.SerializeObject(actual, Formatting.Indented));
                Assert.Inconclusive($"ゴールデンを書き出した。コミットして再実行すること: {goldenPath}");
            }
            var golden = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(goldenPath));
            Assert.That(actual, Is.EquivalentTo(golden));
        }
    }
}
