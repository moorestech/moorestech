using System;
using System.IO;
using System.Security.Cryptography;
using Game.MapGeneration.Facade;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Detail.Filter;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Tests.UnitTest.Game.MapGeneration.Tiling;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain.Golden
{
    /// <summary>
    ///     移設前後で同じ入力を組むための固定フィクスチャ。MultiTileTestWorld の2×2格子に木と岩（クラスタ）を有効化し、
    ///     detail はノイズ変調1エントリ（distanceフィルタ有効・textureフィルタ無効）で端数の重みを作る
    ///     The fixed fixture both sides of the migration build from: MultiTileTestWorld's 2x2 grid with trees and clustered rocks,
    ///     plus one noise-modulated detail entry (distance filter on, texture filter off) to produce fractional weights
    /// </summary>
    public static class TerrainVisualGoldenFixture
    {
        public const int GridSide = 2;
        public const int Seed = 4242;
        public static readonly BiomeType[] BiomeTypes = { BiomeType.Grassland };

        // map.json の vanilla:TestMiningRock。terrainSurroundEffectType=rockBareGroundで実際に岩として振り分けられるguid
        // map.json's vanilla:TestMiningRock; the one guid that actually classifies as rockBareGround
        private const string RockMapObjectGuid = "00000000-0000-2222-0000-000000000001";

        public static string GoldenJsonPath =>
            Path.Combine(Application.dataPath, "Scripts/Client.Tests/UnitTest/Terrain/Golden/terrain_visual_golden.json");

        public static (TerrainGenerationConfig Config, BiomeVisualSections Sections, MapGenerationOutput Output) Build()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            MultiTileTestWorld.EnableTrees(config);
            MultiTileTestWorld.EnableObjects(config);
            config.generateTexture = true;
            config.generateDetail = true;

            // ゴールデンはGrassland単独を固定値とするため、既定で有効なForestをここで無効化する
            // The golden pins Grassland alone, so Forest, enabled by default, is disabled here
            config.forestEnabled = false;

            // EnableObjectsのguidはmap.json上で木扱いのため、実測で確認した岩guidへ差し替える(objectDistanceMap飽和対策)
            // EnableObjects's guids classify as trees in map.json, so swap in the measured rock guid to unsaturate objectDistanceMap
            // 種別は配置元エントリの値が正本(PlacementLedgerTest参照)。guidだけ差し替えても既定のtreeRootPatchのままでは岩に化けない
            // The kind is authoritative from the source entry (see PlacementLedgerTest); swapping only the guid leaves the default treeRootPatch, so it never becomes a rock
            foreach (var entry in config.grassland.objectConfig.entries)
            {
                entry.mapObjectGuids = new[] { RockMapObjectGuid };
                entry.terrainSurroundEffectType = TerrainSurroundEffectType.rockBareGround;
            }

            // 木の根元を塗る樹種にする。塗らないと surround 経路がゴールデンに含まれない
            // Make the species paint its root patch; otherwise the surround path never enters the golden
            foreach (var prototype in config.grassland.treePlacement.prototypes)
            {
                prototype.surroundLayerAddressablePath = "addr/treeRoot";
                prototype.surroundLayerWeight = 0.5f;
                prototype.surroundLayerWidth = 3f;
            }

            var sections = new BiomeVisualSections(
                new[] { "addr/grass" },
                new[] { new BiomeTextureConfig { entries = new TextureEntry[0] } },
                new[] { CreateDetailConfig() },
                new[] { CreateSurroundConfig() });

            // 出力は生成そのもの。木・岩の位置とクラスタは VanillaGenerator が決める
            // The output is generation itself; tree and rock positions and clusters come from VanillaGenerator
            var output = new VanillaGenerator().Generate(config);
            return (config, sections, output);
        }

        public static string Sha256(Array values)
        {
            using var sha256 = SHA256.Create();
            var bytes = new byte[values.Length * 4];
            var index = 0;
            foreach (var value in values)
            {
                var bits = value is float f ? BitConverter.GetBytes(f) : BitConverter.GetBytes((int)value);
                Buffer.BlockCopy(bits, 0, bytes, index, 4);
                index += 4;
            }
            return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static BiomeDetailConfig CreateDetailConfig()
        {
            return new BiomeDetailConfig
            {
                filterRejectThreshold = 0.05f,
                borderMargin = 0f,
                entries = new[]
                {
                    new DetailEntry
                    {
                        prototypeConfig = new DetailPrototypeSpec { usePrototypeMesh = false, prototypeTextureAddressablePath = "addr/grassTex", minWidth = 1f, maxWidth = 2f, minHeight = 1f, maxHeight = 2f },
                        weight = 1f, weightRange = new Vector2(0f, 1f), maxDensity = 8, occludedByOthers = false,
                        noiseStack = new DetailNoiseStack
                        {
                            primary = new DetailNoiseLayer { noiseType = MapNoiseType.Simple, frequency = 0.05f, amplitude = 1f, offset = 0f, balance = 0.5f },
                            secondary = new DetailNoiseLayer { noiseType = MapNoiseType.None }, secondaryOp = NoiseOp.Multiply,
                            tertiary = new DetailNoiseLayer { noiseType = MapNoiseType.None }, tertiaryOp = NoiseOp.Multiply,
                        },
                        slopeFilter = new DetailFilter { enabled = true, mode = DetailFilter.Mode.Simple, weight = 1f, range = new Vector2(0f, 30f), smoothness = new Vector2(2f, 5f), noise = new DetailNoiseLayer { noiseType = MapNoiseType.None } },
                        curvatureFilter = new DetailFilter { enabled = false, noise = new DetailNoiseLayer { noiseType = MapNoiseType.None } },
                        angleFilter = new DetailFilter { enabled = false, noise = new DetailNoiseLayer { noiseType = MapNoiseType.None } },
                        treeDistanceFilter = new DetailFilter { enabled = true, mode = DetailFilter.Mode.Simple, weight = 1f, range = new Vector2(3f, 40f), smoothness = new Vector2(2f, 0f), noise = new DetailNoiseLayer { noiseType = MapNoiseType.None } },
                        objectDistanceFilter = new DetailFilter { enabled = true, mode = DetailFilter.Mode.Simple, weight = 1f, range = new Vector2(5f, 40f), smoothness = new Vector2(3f, 0f), noise = new DetailNoiseLayer { noiseType = MapNoiseType.None } },
                        textureFilter = new DetailTextureFilter { enabled = false, otherTextureWeight = 1f, entries = new DetailTextureFilter.TextureFilterEntry[0] },
                    },
                },
            };
        }

        private static SurroundTextureConfig CreateSurroundConfig()
        {
            return new SurroundTextureConfig
            {
                enabled = true, surroundLayerAddressablePath = "addr/mud",
                coreRadius = 5f, coreBlendMin = 0.8f, coreBlendMax = 0.95f,
                transitionRadius = 15f, transitionBlendMin = 0.15f, transitionBlendMax = 0.5f,
                noiseLowFrequency = 0.03f, noiseHighFrequency = 0.15f, noiseLowWeight = 0.6f,
                rockMeshBaseSize = 5f, singleRockRadius = 8f, singleRockBlend = 0.6f,
            };
        }
    }
}
