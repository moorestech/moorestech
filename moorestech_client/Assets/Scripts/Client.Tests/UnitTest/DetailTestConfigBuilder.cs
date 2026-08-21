using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Detail.Filter;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Tests.UnitTest.Game.MapGeneration.Visual.Surround;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Client.Tests.UnitTest
{
    /// <summary>
    ///     Detailテストが共有する素の設定を組み立てる。各テストは必要な項目だけを有効化する。
    ///     Builds the bare configs the detail tests share; each test enables only what it exercises.
    /// </summary>
    public static class DetailTestConfigBuilder
    {
        // heightmap 5x5 に対し detail は 4x4。両者の解像度差が座標変換のズレを露出させる
        // A 5x5 heightmap yields a 4x4 detail map; the resolution gap exposes coordinate-mapping mistakes
        public const int HeightmapResolution = 5;
        public const int DetailResolution = HeightmapResolution - 1;

        public static TerrainDimensions CreateDimensions()
        {
            return new TerrainDimensions(
                terrainWidth: 100f, terrainLength: 100f, terrainHeight: 50f,
                worldOffsetX: 0f, worldOffsetZ: 0f,
                resolution: HeightmapResolution, seaLevel: 0f, shoreMinHeight: 0f, seed: 1,
                spawnWorldX: 0f, spawnWorldZ: 0f,
                tileIndexX: 0, tileIndexZ: 0, gridSizeX: 1, gridSizeZ: 1);
        }

        public static bool[,] CreateFullMask()
        {
            var mask = new bool[HeightmapResolution, HeightmapResolution];
            for (var z = 0; z < HeightmapResolution; z++)
            for (var x = 0; x < HeightmapResolution; x++)
                mask[z, x] = true;
            return mask;
        }

        public static float[,] CreateFlatSlopes(float slopeDegrees)
        {
            var slopes = new float[HeightmapResolution, HeightmapResolution];
            for (var z = 0; z < HeightmapResolution; z++)
            for (var x = 0; x < HeightmapResolution; x++)
                slopes[z, x] = slopeDegrees;
            return slopes;
        }

        // 全フィルタ無効・ノイズ無効・プロトタイプ解決済みの1エントリ
        // One entry with every filter and noise disabled and its prototype already resolved
        public static DetailEntry CreateEntry(float weight, int maxDensity)
        {
            return new DetailEntry
            {
                prototypeConfig = new DetailPrototypeConfig
                {
                    usePrototypeMesh = false,
                    prototypeTexture = new Texture2D(1, 1),
                    renderMode = DetailRenderMode.Grass,
                },
                weight = weight,
                weightRange = new Vector2(0f, 1f),
                maxDensity = maxDensity,
                occludedByOthers = false,
                noiseStack = CreateInactiveNoiseStack(),
                slopeFilter = CreateDisabledFilter(),
                curvatureFilter = CreateDisabledFilter(),
                angleFilter = CreateDisabledFilter(),
                treeDistanceFilter = CreateDisabledFilter(),
                objectDistanceFilter = CreateDisabledFilter(),
                textureFilter = new DetailTextureFilter { enabled = false, entries = new DetailTextureFilter.TextureFilterEntry[0] },
            };
        }

        public static DetailNoiseStack CreateInactiveNoiseStack()
        {
            return new DetailNoiseStack
            {
                primary = CreateInactiveNoiseLayer(),
                secondary = CreateInactiveNoiseLayer(),
                secondaryOp = NoiseOp.Multiply,
                tertiary = CreateInactiveNoiseLayer(),
                tertiaryOp = NoiseOp.Multiply,
            };
        }

        public static DetailNoiseLayer CreateInactiveNoiseLayer()
        {
            return new DetailNoiseLayer { noiseType = MapNoiseType.None, frequency = 10f, amplitude = 1f };
        }

        // 岩周辺の裸地を使わないテストぶんの実体。null要素のまま渡すとMaxReachへ流れた瞬間にNREになる
        // The instances tests that ignore the bare ground around rocks still need; null elements would NRE the moment MaxReach reads them
        // enabledがfalseでもアドレスは要る。SplatLayerTableは有効無効を見ずに全バイオームぶん登録する
        // The address is required even while disabled: SplatLayerTable registers every biome without consulting the flag
        public static SurroundTextureConfig[] CreateDisabledSurroundConfigs(int biomeCount)
        {
            var surroundConfigs = new SurroundTextureConfig[biomeCount];
            for (var biome = 0; biome < biomeCount; biome++)
                surroundConfigs[biome] = new SurroundTextureConfig
                {
                    surroundLayerAddressablePath = SurroundTestFixtures.MudLayerAddress,
                };

            return surroundConfigs;
        }

        public static DetailFilter CreateDisabledFilter()
        {
            return new DetailFilter
            {
                enabled = false,
                mode = DetailFilter.Mode.Simple,
                weight = 1f,
                range = new Vector2(0f, 90f),
                smoothness = new Vector2(4f, 4f),
                noise = CreateInactiveNoiseLayer(),
                curve = null,
            };
        }
    }
}
