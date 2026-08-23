using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Tiling;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Placement
{
    // スポーン距離帯テストが共有する散布ワールドの組み立てと、距離・中心数の検査。
    // Builds the scatter world shared by the spawn-distance band tests and checks distances and centre counts.
    public static class ObjectScatterBandTestWorld
    {
        public const float NearRadius = 60f;

        private const int Seed = 11;

        // gridSide四方に散布entryを生成、木は出さない。
        // Generate a gridSide-by-gridSide grid with the scatter entry and no trees.
        public static GenerationRun GenerateScatter(int gridSide, bool useClusterMode, params (float OuterRadiusMeters, float Amount)[] bands)
        {
            var config = MultiTileTestWorld.BuildConfig(gridSide, Seed);
            config.generateObject = true;
            config.grassland.objectConfig = BuildScatterConfig(useClusterMode, bands);
            config.forest.objectConfig = BuildScatterConfig(useClusterMode, bands);
            return new VanillaGenerator().Generate(config);
        }

        public static BiomeObjectConfig BuildScatterConfig(bool useClusterMode, (float OuterRadiusMeters, float Amount)[] bands)
        {
            return new BiomeObjectConfig
            {
                entries = new[]
                {
                    new BiomeObjectConfig.ObjectEntry
                    {
                        mapObjectGuids = new[] { MultiTileTestWorld.IndependentMapObjectGuid },
                        placement = BuildPlacement(useClusterMode, bands),
                        scaleRange = new Vector2(1f, 1f),
                        // 既定値のnoneのままだと見た目ステージへ回した瞬間に台帳の代入漏れ検査で落ちる
                        // Leaving the default none would trip the ledger's unset-value check the moment this world feeds a visual stage
                        terrainSurroundEffectType = TerrainSurroundEffectType.rockBareGround,
                    },
                },
            };
        }

        // 同じ帯定義を両モードへ渡すため、量の写し先だけモードで切り替える。
        // The same band definitions feed both modes; only the field the amount lands in changes.
        public static ObjectPlacementParam BuildPlacement(bool useClusterMode, (float OuterRadiusMeters, float Amount)[] bands)
        {
            if (!useClusterMode)
            {
                var scatterBands = new ObjectScatterBand[bands.Length];
                for (var i = 0; i < bands.Length; i++)
                    scatterBands[i] = new ObjectScatterBand
                    {
                        outerRadiusMeters = bands[i].OuterRadiusMeters,
                        pointsPerHectare = bands[i].Amount,
                    };
                return new ObjectScatterParam { bands = scatterBands };
            }

            var clusterBands = new ObjectClusterBand[bands.Length];
            for (var i = 0; i < bands.Length; i++)
                clusterBands[i] = new ObjectClusterBand
                {
                    outerRadiusMeters = bands[i].OuterRadiusMeters,
                    clusterCentersPerHectare = bands[i].Amount,
                };
            return new ObjectClusterParam { bands = clusterBands };
        }

        public static float DistanceFromSpawnXz(Vector3 position, Vector3 spawn)
        {
            return Vector2.Distance(new Vector2(position.x, position.z), new Vector2(spawn.x, spawn.z));
        }

        // 近傍帯クラスタ中心数が「リング面積×density/1万」の桁に収まることを固定する。
        // Poisson散布・マスク・境界除外で下振れはするが、旧clusterCount固定実装が生む極端な過不足は検知する幅を取る。
        // Pins that the near-band centre count lands in the order of ring-area times density over 1e4.
        // Poisson sampling, masking, and edge exclusion can undershoot, but the range still catches the gross over/under-count a fixed clusterCount implementation would produce.
        public static void AssertClusterCenterCountMatchesDensity(GenerationRun run, float density)
        {
            // クラスタ識別子は結果出力ではなく見た目ステージ向けの台帳が持つ（ADR-0025）
            // The cluster identifiers live in the visual-stage ledger rather than the result output (ADR-0025)
            var clusterIds = new HashSet<int>();
            foreach (var placement in run.Ledger.Placements)
                if (placement.Cluster.HasValue) clusterIds.Add(placement.Cluster.Value.Id);

            float ringArea = Mathf.PI * NearRadius * NearRadius;
            int expectedCenters = Mathf.RoundToInt(density * ringArea / 10000f);

            Assert.That(clusterIds.Count, Is.InRange(Mathf.RoundToInt(expectedCenters * 0.3f), Mathf.RoundToInt(expectedCenters * 1.7f)),
                $"cluster centre count {clusterIds.Count} is outside the expected order for density {density} (expected around {expectedCenters})");
        }
    }
}
