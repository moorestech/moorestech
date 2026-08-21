using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.Environment.Terrain;
using Client.Game.InGame.Environment.Terrain.Build;
using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Client.Game.InGame.Environment.Terrain.Visual.Cache;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.MapData;
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
            // MapObjectKindSplitterがguidの種別をMasterHolder経由で引く。ロードしないとNREで落ちる
            // MapObjectKindSplitter resolves a guid's kind through MasterHolder; without loading it this throws an NRE
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void VisualsMatchGolden()
        {
            var (config, sections, output) = TerrainVisualGoldenFixture.Build();
            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech_golden_{System.Guid.NewGuid()}");
            var worldDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);
            TerrainFileWriter.Write(worldDirectory, output);

            // 転送DTOは生成出力から組む。InstanceIdは見た目に効かないので連番
            // Build the wire DTOs from the generation output; InstanceId does not affect visuals, so it is sequential
            var mapObjects = new List<MapObjectLayoutMessagePack>();
            for (var i = 0; i < output.MapObjects.Count; i++)
            {
                var placed = output.MapObjects[i];
                mapObjects.Add(new MapObjectLayoutMessagePack(i, placed.MapObjectGuid,
                    placed.Position.x, placed.Position.y, placed.Position.z,
                    placed.Rotation.x, placed.Rotation.y, placed.Rotation.z, placed.Rotation.w,
                    placed.Scale.x, placed.Scale.y, placed.Scale.z,
                    placed.ClusterId, placed.ClusterCenter.x, placed.ClusterCenter.y));
            }

            var gridConfig = config.ShallowCopy();
            gridConfig.worldOffsetX = output.NoiseOrigin.x;
            gridConfig.worldOffsetZ = output.NoiseOrigin.y;
            var helper = new BiomePlacementHelper(gridConfig);
            var species = TreeSurroundSpeciesTable.Build(helper, TerrainVisualGoldenFixture.BiomeTypes);
            var layerTable = SplatLayerTable.Build("addr/beach", "addr/rock", sections.MainLayerAddresses, sections.TextureConfigs,
                sections.SurroundTextureConfigs, species, System.Array.Empty<string>());
            var provider = new TerrainTileVisualProvider(gridConfig, TerrainVisualGoldenFixture.BiomeTypes, sections, layerTable,
                new TerrainLayer[layerTable.OrderedLayerAddresses.Count], species, mapObjects, worldDirectory,
                new TerrainVisualCache(worldDirectory, new string('0', 64)));

            var actual = new Dictionary<string, string>();
            foreach (var (tileX, tileZ) in TerrainTransferMeta.EnumerateTileCoordinates(output.Tiles.Count))
            {
                var tileConfig = gridConfig.CreateTileConfig(tileX, tileZ);
                var tileScene = config.TileScenePosition(tileX, tileZ);
                var tileWorld = new Vector3(tileScene.x, 0f, tileScene.y);
                var pre = TerrainFileLoader.LoadHeights(worldDirectory, tileX, tileZ, config.Resolution);
                var post = TreePerturbationApplier.Apply(pre, tileConfig, tileWorld, mapObjects);
                var (visual, _) = provider.Resolve(tileX, tileZ, tileConfig, tileWorld, pre, post);
                actual[$"alphamap_{tileX}_{tileZ}"] = TerrainVisualGoldenFixture.Sha256(visual.Alphamap);
                actual[$"heights_{tileX}_{tileZ}"] = TerrainVisualGoldenFixture.Sha256(post);
                for (var d = 0; d < visual.DetailMaps.Count; d++)
                    actual[$"detail_{tileX}_{tileZ}_{d}"] = TerrainVisualGoldenFixture.Sha256(visual.DetailMaps[d]);
            }
            Directory.Delete(worldRoot, true);

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
