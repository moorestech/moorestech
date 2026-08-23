using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Detail.Filter;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Detail
{
    /// <summary>
    ///     Detailが摂動前と摂動後のどちらの高さを読むかを用途別に固定する。移植元は密度を摂動前・傾斜を摂動後から
    ///     採っており（TerrainGenerator.cs:1147,1283）、入れ替えても例外は出ず草の分布だけが静かにずれる
    ///     Pins which heights the detail path reads per purpose: the source takes density from the pre-tree heights and
    ///     slopes from the post-tree ones (TerrainGenerator.cs:1147,1283); swapping them throws nothing and only shifts the grass
    /// </summary>
    public class TerrainDetailBuilderHeightSourceTest
    {
        private const int Resolution = 5;
        private const int DetailResolution = Resolution - 1;
        private const int DetailCellCount = DetailResolution * DetailResolution;
        private const int MaxDensity = 8;
        private const float FlatHeight = 0.5f;

        private static readonly BiomeType[] BiomeTypes = { BiomeType.Grassland };

        [Test]
        public void ComputesTheSlopeFilterFromThePerturbedHeights()
        {
            // 傾斜フィルタは緩斜面しか通さない。急斜面が摂動後にしか無いので、どちらを傾斜へ渡したかで結果が反転する
            // The slope filter admits gentle ground only; the steep ramp exists solely in the post-tree heights, so the outcome flips with the argument
            var fromFlatSlopes = Build(CreateSlopeFilteredSections(), CreateRamp(), CreateFlat());
            var fromSteepSlopes = Build(CreateSlopeFilteredSections(), CreateFlat(), CreateRamp());

            // 摂動後が急斜面のとき残るのは最終列だけ。ComputeSlopeは前進差分で、右隣が無い列の勾配が0になるため
            // Only the last column survives a steep post-tree height: ComputeSlope is a forward difference and the column without a right neighbour reads zero
            Assert.That(TotalDensity(fromFlatSlopes), Is.EqualTo(DetailCellCount * MaxDensity), "摂動後が平坦なら全画素が通る");
            Assert.That(TotalDensity(fromSteepSlopes), Is.EqualTo(DetailResolution * MaxDensity), "摂動後が急斜面なら最終列以外が落ちる");
        }

        [Test]
        public void ComputesTheCurvatureFilterFromThePreTreeHeights()
        {
            // 曲率は密度側の入力から採る。曲率フィルタは平坦(正規化0.5)だけを通すので、椀型を渡した側が落ちる
            // Curvature comes from the density-side input; the filter admits only flatness (normalized 0.5), so the bowl-fed side drops out
            var fromFlatCurvature = Build(CreateCurvatureFilteredSections(), CreateFlat(), CreateBowl());
            var fromBowlCurvature = Build(CreateCurvatureFilteredSections(), CreateBowl(), CreateFlat());

            Assert.That(TotalDensity(fromFlatCurvature), Is.EqualTo(DetailCellCount * MaxDensity), "摂動前が平坦なら全画素が通る");
            Assert.That(TotalDensity(fromBowlCurvature), Is.EqualTo(0), "摂動前が椀型なら曲率フィルタで落ちる");
        }

        private static int TotalDensity(List<int[,]> maps)
        {
            var total = 0;
            foreach (var map in maps)
                for (var z = 0; z < map.GetLength(0); z++)
                for (var x = 0; x < map.GetLength(1); x++)
                    total += map[z, x];

            return total;
        }

        private static List<int[,]> Build(
            BiomeVisualSections visualSections, float[,] preHeights, float[,] postHeights)
        {
            return TerrainDetailBuilder.Build(
                CreateConfig(), BiomeTypes, visualSections, preHeights, postHeights, CreateWinnerMasks(), null,
                new LedgerPlacement[0], Vector3.zero, 0, 0);
        }

        private static BiomeVisualSections CreateSlopeFilteredSections()
        {
            var entry = DetailTestConfigBuilder.CreateEntry(1f, MaxDensity);
            entry.slopeFilter = CreateBandFilter(0f, 5f, 1f);
            return CreateSections(entry);
        }

        private static BiomeVisualSections CreateCurvatureFilteredSections()
        {
            var entry = DetailTestConfigBuilder.CreateEntry(1f, MaxDensity);
            entry.curvatureFilter = CreateBandFilter(0.45f, 0.55f, 0.01f);
            return CreateSections(entry);
        }

        private static DetailFilter CreateBandFilter(float rangeMin, float rangeMax, float smoothness)
        {
            var filter = DetailTestConfigBuilder.CreateDisabledFilter();
            filter.enabled = true;
            filter.range = new Vector2(rangeMin, rangeMax);
            filter.smoothness = new Vector2(smoothness, smoothness);
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
                overrideResolution = Resolution, detailResolution = Resolution - 1, seed = 4321,
                terrainWidth = 100f, terrainLength = 100f, terrainHeight = 50f,
            };
        }

        // 唯一のバイオームがタイル全面で勝つ配置。高さの取り違えだけを観測するためマスクは一様にする
        // The single biome wins the whole tile; a uniform mask keeps the observation on the height mix-up alone
        private static bool[][,] CreateWinnerMasks()
        {
            var mask = new bool[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                mask[z, x] = true;

            return new[] { mask };
        }

        private static float[,] CreateFlat()
        {
            var heights = new float[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                heights[z, x] = FlatHeight;

            return heights;
        }

        // 25mあたり12.5m上がる斜面。約26度で緩斜面帯(0-5度)からはっきり外れる
        // A ramp rising 12.5m per 25m: about 26 degrees, clearly outside the 0-5 degree gentle band
        private static float[,] CreateRamp()
        {
            var heights = new float[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                heights[z, x] = x / (float)(Resolution - 1);

            return heights;
        }

        // 中央が凹んだ椀型。正規化曲率が0か1へ振れ、平坦の0.5とはっきり分かれる
        // A bowl dipping at the center; its normalized curvature swings to 0 or 1, well clear of flatness at 0.5
        private static float[,] CreateBowl()
        {
            var heights = new float[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                heights[z, x] = (x - 2) * (x - 2) * 0.1f;

            return heights;
        }
    }
}
