using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Spawn
{
    // 本番生成前に草原-森林隣接の良地を探索し、スポーン地点 S と中央化オフセット G=S-spawnTarget を算出する。
    // 段1(粗探索)で候補を発見し、段2(本番一致 final 検証)で確定する。分類関数は 5b シームに委譲する。
    // Searches for good grassland-forest adjacency before generation, computing spawn S and
    // centering offset G=S-spawnTarget. Stage1 finds candidates, stage2 verifies; classification
    // is delegated to the 5b seam.
    public static class SpawnRegionFinder
    {
        public static SpawnSearchResult Find(
            TerrainGenerationConfig config, BiomeType[] biomeTypes)
        {
            var sb = new StringBuilder();
            var ss = config.spawnSearch;
            Vector2 gridCenter = SpawnRegionGeometry.GridCenterWorld(config);
            Vector2 spawnTarget = ss.overrideSpawnScenePosition ? ss.spawnScenePosition : gridCenter;

            if (ss.scanCellSize <= 0f || ss.topK <= 0)
            {
                Debug.LogError("[SpawnSearch] scanCellSize/topK invalid (<=0); aborting to fallback.");
                return new SpawnSearchResult(false, Vector2.zero, spawnTarget, 0f, "invalid spawnSearch params");
            }

            int grassBi = System.Array.IndexOf(biomeTypes, BiomeType.Grassland);
            int forestBi = System.Array.IndexOf(biomeTypes, BiomeType.Forest);
            if (grassBi < 0 || forestBi < 0)
                return new SpawnSearchResult(false, Vector2.zero, spawnTarget, 0f,
                    "Grassland/Forest not in active biomes");

            float extent = ss.scanExtent > 0f ? ss.scanExtent : SpawnRegionGeometry.DefaultScanExtent(config);

            // 描画位置が本番サンプル格子に乗っているか確認（中央化の本番一致保証の前提）。
            // Verify the draw position lies on the production sample lattice (centering guarantee).
            float pX = config.terrainWidth / (config.Resolution - 1);
            float pZ = config.terrainLength / (config.Resolution - 1);
            bool latticeX = Mathf.Abs(spawnTarget.x / pX - Mathf.Round(spawnTarget.x / pX)) < 1e-3f;
            bool latticeZ = Mathf.Abs(spawnTarget.y / pZ - Mathf.Round(spawnTarget.y / pZ)) < 1e-3f;
            if (!latticeX || !latticeZ)
            {
                string warn = $"[SpawnSearch] warning: spawn draw position ({spawnTarget.x:F2},{spawnTarget.y:F2}) is off the production lattice (pX={pX:F4},pZ={pZ:F4}).";
                Debug.LogWarning(warn);
                sb.AppendLine(warn);
            }

            for (int iter = 0; iter <= ss.maxExpandIterations; iter++)
            {
                sb.AppendLine($"[iter {iter}] extent={extent:F0}m cell={ss.scanCellSize:F0}m");

                // 段1: 粗探索（分類は 5b シーム）。
                // Stage 1: coarse scan (classification via 5b seam).
                var grid = SpawnClassificationSeam.ClassifyRawGrid(
                    config, biomeTypes, gridCenter.x, gridCenter.y, extent, ss.scanCellSize);
                var candidates = FindCandidates(grid, grassBi, forestBi, ss, sb);

                candidates.Sort((a, b) =>
                {
                    int c = b.Score.CompareTo(a.Score);
                    if (c != 0) return c;
                    c = a.ApproxCenterWorld.x.CompareTo(b.ApproxCenterWorld.x);
                    if (c != 0) return c;
                    return a.ApproxCenterWorld.y.CompareTo(b.ApproxCenterWorld.y);
                });
                var verified = new List<(SpawnSearchResult res, float finalScore, Vector2 spawn)>();
                int verifiedCount = 0;
                foreach (var cand in candidates)
                {
                    verifiedCount++;
                    var vr = SpawnRegionVerifier.Verify(config, biomeTypes, grassBi, forestBi, cand, ss, spawnTarget, sb);
                    if (vr.HasValue) verified.Add(vr.Value);
                    if (verifiedCount >= ss.topK && verified.Count > 0) break;
                }

                if (verified.Count > 0)
                {
                    verified.Sort((a, b) =>
                    {
                        int c = b.finalScore.CompareTo(a.finalScore);
                        if (c != 0) return c;
                        c = a.spawn.x.CompareTo(b.spawn.x);
                        if (c != 0) return c;
                        return a.spawn.y.CompareTo(b.spawn.y);
                    });
                    sb.AppendLine($"selected: score={verified[0].finalScore:F1} (verified {verifiedCount})");
                    var best = verified[0].res;
                    return new SpawnSearchResult(true, best.WorldOffset, best.SpawnWorldPosition,
                        verified[0].finalScore, sb.ToString());
                }

                if (verifiedCount >= ss.topK)
                    sb.AppendLine($"search stopped: verified topK={ss.topK} with no valid");
                extent *= ss.expandFactor;
            }

            sb.AppendLine("no candidate -> zero-offset fallback");
            return new SpawnSearchResult(false, Vector2.zero, spawnTarget, 0f, sb.ToString());
        }

        // 段1: CC 抽出 + 隣接ペアスコアリング。
        // Stage 1: CC extraction plus adjacency-pair scoring.
        static List<SpawnCandidate> FindCandidates(
            CoarseBiomeGrid grid, int grassBi, int forestBi, SpawnSearchConfig ss, StringBuilder sb)
        {
            float cellArea = grid.CellSize * grid.CellSize;
            var grassComps = ConnectedComponents.Label(grid.BiomeIndex, grid.Width, grid.Height, v => v == grassBi);
            var forestComps = ConnectedComponents.Label(grid.BiomeIndex, grid.Width, grid.Height, v => v == forestBi);

            var result = new List<SpawnCandidate>();
            foreach (var g in grassComps)
            {
                if (g.Area * cellArea < ss.minGrasslandArea) continue;

                ConnectedComponents.Component bestForest = null;
                float bestContactM = 0f;
                foreach (var f in forestComps)
                {
                    if (f.Area * cellArea < ss.minForestArea) continue;
                    int contactCells = ConnectedComponents.BorderContactCells(g, f, grid.BiomeIndex, grid.Width, grid.Height);
                    float contactM = contactCells * grid.CellSize;
                    if (contactM > bestContactM) { bestContactM = contactM; bestForest = f; }
                }
                if (bestForest == null || bestContactM < ss.minBorderContact) continue;

                Vector2 centroid = SpawnRegionGeometry.CentroidWorld(g, grid);
                float grassM2 = g.Area * cellArea;
                float forestM2 = bestForest.Area * cellArea;
                float score = ss.wGrasslandArea * grassM2
                            + ss.wForestArea * forestM2
                            + ss.wBorderContact * bestContactM;
                result.Add(new SpawnCandidate
                {
                    ApproxCenterWorld = centroid,
                    BBoxCenterWorld = new Vector2(
                        grid.OriginX + ((g.MinX + g.MaxX) * 0.5f) * grid.CellSize,
                        grid.OriginZ + ((g.MinY + g.MaxY) * 0.5f) * grid.CellSize),
                    Score = score,
                    BBoxW = (g.MaxX - g.MinX + 1) * grid.CellSize,
                    BBoxH = (g.MaxY - g.MinY + 1) * grid.CellSize,
                    SourceGrid = grid,
                    Stage1GrassCells = g.Cells
                });
            }
            sb.AppendLine($"  stage1 candidates: {result.Count}");
            return result;
        }
    }
}
