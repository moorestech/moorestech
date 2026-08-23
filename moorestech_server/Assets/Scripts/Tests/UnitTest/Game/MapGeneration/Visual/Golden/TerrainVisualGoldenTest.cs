using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Golden
{
    /// <summary>
    ///     見た目（alphamap・detail密度・表示用高さ）のSHA256を固定する。TileVisualBakerを直に叩き、
    ///     クライアント経由の移設前と同じハッシュ値を維持する（見た目を1ピクセルも変えない）
    ///     Pins the visuals (alphamap, detail density, display heights) as SHA256, hitting TileVisualBaker directly
    ///     and keeping the exact same hash values as the pre-migration client-side test (not one pixel changes)
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
            var (config, sections, run) = TerrainVisualGoldenFixture.Build();
            var output = run.Output;
            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech_golden_{System.Guid.NewGuid()}");
            var worldDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);
            var actual = new Dictionary<string, string>();

            // finallyで一時ディレクトリを必ず片付ける。途中でResolveが例外を投げても取り残さない
            // The finally block always cleans the temp directory, even if Resolve throws partway through
            try
            {
                TerrainFileWriter.Write(worldDirectory, output);

                var gridConfig = config.ShallowCopy();
                gridConfig.worldOffsetX = output.NoiseOrigin.x;
                gridConfig.worldOffsetZ = output.NoiseOrigin.y;
                var helper = new BiomePlacementHelper(gridConfig);
                var species = TreeSurroundSpeciesTable.Build(helper, TerrainVisualGoldenFixture.BiomeTypes);
                var layerTable = SplatLayerTable.Build("addr/beach", "addr/rock", sections.MainLayerAddresses, sections.TextureConfigs,
                    sections.SurroundTextureConfigs, species, System.Array.Empty<string>());
                var baker = new TileVisualBaker(gridConfig, TerrainVisualGoldenFixture.BiomeTypes, sections, layerTable,
                    species, new MaterializedPlacementLedgerSource(run.Ledger), worldDirectory,
                    new TerrainVisualCache(worldDirectory, new string('0', 64)));

                foreach (var (tileX, tileZ) in TerrainTransferMeta.EnumerateTileCoordinates(output.Tiles.Count))
                {
                    var baked = baker.Bake(tileX, tileZ);
                    actual[$"alphamap_{tileX}_{tileZ}"] = TerrainVisualGoldenFixture.Sha256(baked.AlphamapPlanes.SelectMany(plane => plane).ToArray());
                    actual[$"heights_{tileX}_{tileZ}"] = TerrainVisualGoldenFixture.Sha256(baked.DisplayHeights);
                    for (var d = 0; d < baked.DetailMaps.Count; d++)
                        actual[$"detail_{tileX}_{tileZ}_{d}"] = TerrainVisualGoldenFixture.Sha256(baked.DetailMaps[d]);
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
