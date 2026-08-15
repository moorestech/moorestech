using Client.Game.InGame.Environment.Terrain.Visual.Splat;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using Game.MapGeneration.Pipeline.Config;
using Server.Protocol.PacketResponse.MapData;

namespace Client.Tests.UnitTest.Terrain.Surround
{
    /// <summary>
    ///     岩周辺の裸地テストが共有する小さな地形。40m四方・alphamap8画素なので1画素は40/7mで、
    ///     岩は画素4の中心にちょうど乗る。遷移帯15mが数画素に届く寸法に揃えてある
    ///     The small terrain the bare-ground tests share: 40m square over an 8-pixel alphamap, so a pixel spans 40/7m
    ///     and the rock lands exactly on pixel 4's center, sized so a 15m transition band reaches several pixels
    /// </summary>
    public static class SurroundTestFixtures
    {
        public const int Resolution = 9;
        public const int AlphaResolution = 8;
        public const float TerrainSize = 40f;
        public const int LayerCount = 3;

        // Mudを含むアドレスが来る列。フォールバック探索が引き当てるべき唯一の索引
        // The column holding the Mud-bearing address, the one index the fallback search must find
        public const int MudLayerIndex = 1;

        public const int RockPixel = 4;
        public const float RockLocalPosition = TerrainSize * RockPixel / (AlphaResolution - 1);

        public static TerrainGenerationConfig CreateConfig()
        {
            return new TerrainGenerationConfig
            {
                overrideResolution = Resolution,
                terrainWidth = TerrainSize,
                terrainLength = TerrainSize,
                terrainHeight = 600f,
            };
        }

        public static SplatLayerTable CreateLayerTable()
        {
            return SplatLayerTable.Build(
                "addr/beach", "addr/Mud01", new[] { "addr/grass" },
                new[] { new BiomeTextureConfig { entries = new TextureEntry[0] } },
                new[] { new SurroundTextureConfig() });
        }

        public static SurroundTextureConfig CreateSurroundConfig()
        {
            return new SurroundTextureConfig
            {
                enabled = true,
                surroundLayerAddressablePath = string.Empty,
                coreRadius = 5f,
                coreBlendMin = 0.8f,
                coreBlendMax = 0.95f,
                transitionRadius = 15f,
                transitionBlendMin = 0.15f,
                transitionBlendMax = 0.5f,
                noiseLowFrequency = 0.03f,
                noiseHighFrequency = 0.15f,
                noiseLowWeight = 0.6f,
                rockMeshBaseSize = 5f,
                singleRockRadius = 8f,
                singleRockBlend = 0.6f,
            };
        }

        // Scaleは2倍。フットプリント半径は (2+2)/2 * rockMeshBaseSize になる
        // The scale is doubled, making the footprint radius (2+2)/2 * rockMeshBaseSize
        public static MapObjectLayoutMessagePack CreateRock(int clusterId)
        {
            return new MapObjectLayoutMessagePack(
                1, "00000000-0000-0000-0000-000000000001",
                RockLocalPosition, 0f, RockLocalPosition, 2f, 2f, 2f,
                clusterId,
                clusterId < 0 ? 0f : RockLocalPosition,
                clusterId < 0 ? 0f : RockLocalPosition);
        }

        // 指定バイオームだけが重み1を持つ配置用重み。列オフセット+2はOcean/Beach列ぶん
        // Placement weights giving weight 1 to one biome only; the +2 column offset covers Ocean and Beach
        public static float[,] CreateBiomeWeights(int winningBiome)
        {
            var weights = new float[Resolution * Resolution, 2 + 2];
            for (var pixel = 0; pixel < Resolution * Resolution; pixel++)
                weights[pixel, 2 + winningBiome] = 1f;

            return weights;
        }

        public static float[,] CreateHeights(float slopeAlongX)
        {
            var heights = new float[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                heights[z, x] = x / (float)(Resolution - 1) * slopeAlongX;

            return heights;
        }

        // レイヤー2に全重みを寄せた初期状態。裸地が乗ればここが削れてMud側へ移る
        // A start state with all weight on layer 2; bare ground eats into it and moves onto Mud
        public static float[,,] CreateUniformAlphamap()
        {
            var alphamap = new float[AlphaResolution, AlphaResolution, LayerCount];
            for (var z = 0; z < AlphaResolution; z++)
            for (var x = 0; x < AlphaResolution; x++)
                alphamap[z, x, 2] = 1f;

            return alphamap;
        }
    }
}
