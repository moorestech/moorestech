using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Client.Game.InGame.Environment.Terrain.Visual.Detail;
using Client.Game.InGame.Environment.Terrain.Visual.Source;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Server.Boot;
using Server.Protocol.PacketResponse.MapData;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain.Surround
{
    /// <summary>
    ///     結線テストが共有する1タイルぶんの入力を、シーン絶対座標の岩と原点から離れたタイル原点で組み立てる。
    ///     岩の座標をタイルローカルで書けるようにしつつ、Generateには必ずシーン絶対座標で渡す
    ///     Builds the single-tile input the wiring tests share, with scene-absolute rocks and a tile origin away from zero;
    ///     rock positions stay writable in tile-local terms while Generate always receives them scene-absolute
    /// </summary>
    public static class SurroundWiringTestScene
    {
        public const int Resolution = 9;

        // 100mのタイルを11画素で割るので1画素10m。境界の外の距離を画素数で書けるようにした寸法
        // A 100m tile over 11 pixels spans 10m each, so distances past the seam stay expressible in pixels
        public const int AlphaResolution = 11;
        public const float TileSize = 100f;

        // レイヤー並びは beach0 / rock1 / grass2 / Mud3。裸地はこの最後の列へ乗る
        // The layer order is beach 0, rock 1, grass 2 and Mud 3, and the bare ground lands on the last column
        public const int MudLayerIndex = 3;

        // 遷移帯15m + フットプリント半径(Scale1×rockMeshBaseSize5)。切り出しhaloはこの20mでなければならない
        // The 15m transition band plus the footprint radius (scale 1 by rockMeshBaseSize 5); the slice halo must be this 20m
        public const float ExpectedMaxReach = 20f;

        // タイルの西辺の中央。ここへ届くかどうかだけで境界の断裂が判定できる
        // The middle of the tile's west edge, where reaching or not decides whether the seam breaks
        public const float SeamLocalZ = 50f;
        public const int SeamPixelZ = 5;
        public const int SeamPixelX = 0;

        private const string MudLayerAddress = "addr/MudDry";
        private const string StoneGuid = "00000000-0000-2222-0000-000000000001";
        private const int ClusterId = 7;

        // 原点から離れ、Zが負のタイル。タイル原点やローカル化を取り違えると岩が切り出しから丸ごと落ちる
        // A tile away from the origin with a negative Z; a wrong tile origin or rebasing drops the rock from the slice entirely
        public static readonly Vector3 TileWorldPosition = new(300f, 0f, -200f);

        private static readonly BiomeType[] BiomeTypes = { BiomeType.Grassland };

        // MapObjectKindSplitterがsoundEffectTypeを引くためMasterHolderが要る
        // MapObjectKindSplitter reads soundEffectType, so MasterHolder must be loaded
        public static void LoadMasterData()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        public static float[,,] Generate(params MapObjectLayoutMessagePack[] mapObjects)
        {
            var config = CreateConfig();
            var visualSections = CreateVisualSections();
            var layerTable = SplatLayerTable.Build(
                "addr/beach", "addr/rock", visualSections.MainLayerAddresses, visualSections.TextureConfigs,
                visualSections.SurroundTextureConfigs);

            using var classification = new TerrainClassificationContext(config, BiomeTypes);
            classification.Initialize();

            return SplatmapRuntimeGenerator.Generate(
                config, BiomeTypes, classification, layerTable, visualSections,
                CreateHeights(), CreateBiomeIndices(), AlphaResolution, mapObjects, TileWorldPosition);
        }

        // 引数はタイルローカル。シーン絶対座標へ戻して渡し、切り出しのローカル化まで通しで動かす
        // The arguments are tile-local and pushed back to scene-absolute, running the slicer's rebasing end to end
        public static MapObjectLayoutMessagePack CreateStone(float localX, float localZ)
        {
            var worldX = TileWorldPosition.x + localX;
            var worldZ = TileWorldPosition.z + localZ;

            return new MapObjectLayoutMessagePack(
                1, StoneGuid, worldX, 0f, worldZ, 1f, 1f, 1f, ClusterId, worldX, worldZ);
        }

        public static SurroundTextureConfig CreateSurroundConfig()
        {
            return new SurroundTextureConfig
            {
                enabled = true,
                surroundLayerAddressablePath = MudLayerAddress,
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

        private static BiomeVisualSections CreateVisualSections()
        {
            return new BiomeVisualSections(
                new[] { "addr/grass" },
                new[] { new BiomeTextureConfig { entries = new TextureEntry[0] } },
                new[] { new BiomeDetailConfig { entries = new DetailEntry[0] } },
                new[] { CreateSurroundConfig() });
        }

        private static TerrainGenerationConfig CreateConfig()
        {
            return new TerrainGenerationConfig
            {
                overrideResolution = Resolution,
                seed = 12345,
                terrainWidth = TileSize,
                terrainLength = TileSize,
                terrainHeight = 600f,
                grasslandEnabled = true,
                forestEnabled = false,
                savannaEnabled = false,
                desertEnabled = false,
                mesaEnabled = false,
                alpineEnabled = false,
                jungleEnabled = false,
                woodsEnabled = false,
            };
        }

        // x方向に上がる傾斜。傾斜バイアスが一様入力で潰れないようにする
        // A slope rising along x so the downhill bias is not fed a flat input
        private static float[,] CreateHeights()
        {
            var heights = new float[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                heights[z, x] = x / (float)(Resolution - 1) * 0.5f;

            return heights;
        }

        private static byte[,] CreateBiomeIndices()
        {
            var biomeIndices = new byte[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                biomeIndices[z, x] = (byte)BiomeType.Grassland;

            return biomeIndices;
        }
    }
}
