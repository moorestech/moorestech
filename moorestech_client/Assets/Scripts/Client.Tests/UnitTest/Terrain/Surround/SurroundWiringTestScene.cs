using System;
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

        // レイヤー並びは beach0 / rock1 / grass2 / Mud3 / Dirt4。岩の裸地と木の根元は別の列へ乗る
        // The layer order is beach 0, rock 1, grass 2, Mud 3 and Dirt 4, keeping the rocks' bare ground and the tree roots on separate columns
        public const int MudLayerIndex = 3;
        public const int TreeRootLayerIndex = 4;

        // 遷移帯15m + フットプリント半径(Scale1×rockMeshBaseSize5)。切り出しhaloはこの20mでなければならない
        // The 15m transition band plus the footprint radius (scale 1 by rockMeshBaseSize 5); the slice halo must be this 20m
        public const float ExpectedMaxReach = 20f;

        // 木の根元は幅30m=3画素ぶん届く。岩の20mより広いので、岩のMaxReachを使い回すと境界の外の木が落ちる
        // A tree's root reaches its 30m width, three pixels; wider than the rocks' 20m, so reusing their MaxReach drops trees past the seam
        public const float TreeSurroundWidth = 30f;

        // タイルの西辺の中央。ここへ届くかどうかだけで境界の断裂が判定できる
        // The middle of the tile's west edge, where reaching or not decides whether the seam breaks
        public const float SeamLocalZ = 50f;
        public const int SeamPixelZ = 5;
        public const int SeamPixelX = 0;

        public const string TreeRootLayerAddress = "addr/Dirt";
        public const string TreeGuid = "00000000-0000-1111-0000-000000000001";

        // 岩のguidも樹種テーブルに載せる（SurroundWiringTestConfig）。載せないと振り分けの取り違えをテーブル引きが黙って救ってしまう
        // The rock's guid is mapped as a species too (SurroundWiringTestConfig); otherwise a failed lookup would quietly rescue a mis-sorted rock
        public const string StoneGuid = "00000000-0000-2222-0000-000000000001";

        private const string MudLayerAddress = "addr/MudDry";
        private const int ClusterId = 7;

        // 原点から離れ、Zが負のタイル。タイル原点やローカル化を取り違えると岩が切り出しから丸ごと落ちる
        // A tile away from the origin with a negative Z; a wrong tile origin or rebasing drops the rock from the slice entirely
        public static readonly Vector3 TileWorldPosition = new(300f, 0f, -200f);

        private static readonly BiomeType[] BiomeTypes = { BiomeType.Grassland };

        // MapObjectKindSplitterがterrainSurroundEffectTypeを引くためMasterHolderが要る
        // MapObjectKindSplitter reads terrainSurroundEffectType, so MasterHolder must be loaded
        public static void LoadMasterData()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        public static float[,,] Generate(params MapObjectLayoutMessagePack[] mapObjects)
        {
            var config = SurroundWiringTestConfig.Create();
            var visualSections = CreateVisualSections();

            // 本番と同じく樹種テーブルを列と塗りの双方へ渡す。片方だけ別物を注げないのはこの型の持ち分
            // The species table feeds both the columns and the painting as production does; that only one can be supplied is the type's own doing
            var treeSurroundSpecies = TreeSurroundSpecies();
            var layerTable = SplatLayerTable.Build(
                "addr/beach", "addr/rock", visualSections.MainLayerAddresses, visualSections.TextureConfigs,
                visualSections.SurroundTextureConfigs, treeSurroundSpecies, Array.Empty<string>());

            using var classification = new TerrainClassificationContext(config, BiomeTypes);
            classification.Initialize();

            return SplatmapRuntimeGenerator.Generate(
                config, BiomeTypes, classification, layerTable, visualSections, treeSurroundSpecies,
                CreateHeights(), CreateBiomeIndices(), AlphaResolution, mapObjects, TileWorldPosition);
        }

        public static TreeSurroundSpeciesTable TreeSurroundSpecies()
        {
            return TreeSurroundSpeciesTable.Build(
                new BiomePlacementHelper(SurroundWiringTestConfig.Create()), BiomeTypes);
        }

        // 引数はタイルローカル。シーン絶対座標へ戻して渡し、切り出しのローカル化まで通しで動かす
        // The arguments are tile-local and pushed back to scene-absolute, running the slicer's rebasing end to end
        public static MapObjectLayoutMessagePack CreateStone(float localX, float localZ)
        {
            var worldX = TileWorldPosition.x + localX;
            var worldZ = TileWorldPosition.z + localZ;

            return new MapObjectLayoutMessagePack(
                1, StoneGuid, worldX, 0f, worldZ, 0f, 0f, 0f, 1f, 1f, 1f, 1f, ClusterId, worldX, worldZ);
        }

        // 木もタイルローカルで書いてシーン絶対座標へ戻す。クラスタは持たないので独立配置(-1)で置く
        // A tree is written tile-local and pushed back to scene-absolute too; it owns no cluster and goes in as an independent placement (-1)
        public static MapObjectLayoutMessagePack CreateTree(float localX, float localZ)
        {
            return new MapObjectLayoutMessagePack(
                2, TreeGuid, TileWorldPosition.x + localX, 0f, TileWorldPosition.z + localZ,
                0f, 0f, 0f, 1f, 1f, 1f, 1f, -1, 0f, 0f);
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
