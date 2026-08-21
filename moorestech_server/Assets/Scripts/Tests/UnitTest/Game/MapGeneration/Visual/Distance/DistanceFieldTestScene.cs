using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Detail.Filter;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.MapGeneration.Visual.Detail;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Distance
{
    /// <summary>
    ///     距離場のビルダーテストが共有する1タイルぶんの入力を組み立てる。
    ///     供給の検証とhalo窓の検証で同じ盤面を使うため、盤面の寸法と閾値をここに1本化する
    ///     Builds the single-tile input the distance-field builder tests share;
    ///     the supply and halo-window suites run on the same board, so its extents and thresholds live here
    /// </summary>
    public static class DistanceFieldTestScene
    {
        public const int Resolution = 5;
        public const int DetailResolution = Resolution - 1;
        public const int MaxDensity = 8;
        public const float TileSize = 100f;

        // 距離フィルタは10m以遠だけを通す。smoothnessが0なので探索半径もhaloもrange.yと同値になる
        // The filter admits 10m and beyond; with zero smoothness both the search radius and the halo equal range.y
        public const float RejectedWithin = 10f;
        public const float SearchRadius = 200f;

        public const string TreeGuid = "00000000-0000-1111-0000-000000000001";
        public const string StoneGuid = "00000000-0000-2222-0000-000000000001";

        // 原点から離れた負のZを持つタイル。ローカル化の取り違えを距離値の桁で露出させる
        // A tile away from the origin with a negative Z, so a rebasing mistake shows up as an order-of-magnitude distance
        public static readonly Vector3 TilePosition = new(100f, 0f, -200f);

        private static readonly BiomeType[] BiomeTypes = { BiomeType.Grassland };

        // このフィクスチャを使うテストの前提としてMasterHolderをロードしておく
        // Loads MasterHolder as a precondition for the tests that use this fixture
        public static void LoadMasterData()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        public static List<int[,]> Build(BiomeVisualSections visualSections, params LedgerPlacement[] placements)
        {
            var config = CreateConfig();
            var heights = new float[Resolution, Resolution];

            return TerrainDetailBuilder.Build(
                config, BiomeTypes, visualSections, heights, heights, CreateWinnerMasks(), null,
                placements, TilePosition, 0, 0);
        }

        public static BiomeVisualSections TreeDistanceSections()
        {
            var entry = DetailTestConfigBuilder.CreateEntry(1f, MaxDensity);
            entry.treeDistanceFilter = CreateDistanceFilter();
            return CreateSections(entry);
        }

        public static BiomeVisualSections ObjectDistanceSections()
        {
            var entry = DetailTestConfigBuilder.CreateEntry(1f, MaxDensity);
            entry.objectDistanceFilter = CreateDistanceFilter();
            return CreateSections(entry);
        }

        // 引数はタイルローカル。シーン絶対座標へ戻して渡し、切り出しのローカル化まで通しで検証する
        // The arguments are tile-local and get pushed back to scene-absolute, exercising the slicer's rebasing end to end
        public static LedgerPlacement CreateMapObject(string mapObjectGuid, float localX, float localZ)
        {
            // 種別はguidから直接決める。台帳はマスタ参照済みで既に種別を運んでいるため
            // The kind is decided straight from the guid; the ledger already carries it, having resolved the master itself
            var effect = mapObjectGuid == TreeGuid
                ? TerrainSurroundEffectType.treeRootPatch
                : TerrainSurroundEffectType.rockBareGround;

            return new LedgerPlacement(mapObjectGuid,
                new Vector3(TilePosition.x + localX, 0f, TilePosition.z + localZ),
                Quaternion.identity, Vector3.one, effect, -1, Vector2.zero);
        }

        // detail画素xのワールド座標。SdfMapGeneratorの割り付けと同じ式で、距離の期待値を式で書けるようにする
        // The world coordinate of detail pixel x, matching SdfMapGenerator's mapping so expected distances stay expressible
        public static float PixelWorldCoordinate(int index)
        {
            return (float)index / (DetailResolution - 1) * TileSize;
        }

        private static DetailFilter CreateDistanceFilter()
        {
            var filter = DetailTestConfigBuilder.CreateDisabledFilter();
            filter.enabled = true;
            filter.range = new Vector2(RejectedWithin, SearchRadius);
            filter.smoothness = Vector2.zero;
            return filter;
        }

        private static BiomeVisualSections CreateSections(DetailEntry entry)
        {
            var detailConfig = new BiomeDetailConfig
            {
                entries = new[] { entry }, filterRejectThreshold = 0.01f, borderMargin = 0f,
            };

            return new BiomeVisualSections(
                new string[BiomeTypes.Length], new BiomeTextureConfig[BiomeTypes.Length], new[] { detailConfig },
                DetailTestConfigBuilder.CreateDisabledSurroundConfigs(BiomeTypes.Length));
        }

        private static TerrainGenerationConfig CreateConfig()
        {
            return new TerrainGenerationConfig
            {
                overrideResolution = Resolution, seed = 4321,
                terrainWidth = TileSize, terrainLength = TileSize, terrainHeight = 50f,
            };
        }

        private static bool[][,] CreateWinnerMasks()
        {
            var mask = new bool[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                mask[z, x] = true;

            return new[] { mask };
        }
    }
}
