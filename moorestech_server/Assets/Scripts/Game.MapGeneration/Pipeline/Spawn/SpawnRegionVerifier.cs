using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Spawn
{
    // 段2: 本番一致 final 検証 + pole of inaccessibility による確定。
    // Stage 2: production-matching final verification plus pole-of-inaccessibility selection.
    internal static class SpawnRegionVerifier
    {
        public static (SpawnSearchResult res, float finalScore, Vector2 spawn)? Verify(
            TerrainGenerationConfig config, BiomeType[] biomeTypes, int grassBi, int forestBi,
            SpawnCandidate cand, SpawnSearchConfig ss, Vector2 spawnTarget, StringBuilder sb)
        {
            // OOM 防止のため窓のメートル上限を解像度から逆算する。
            // Derive the window meter cap from resolution to prevent OOM.
            float pXLocal = config.terrainWidth / (config.Resolution - 1);
            float maxWindow = (ss.maxDetailedResolution - 1) * pXLocal;
            float windowSize = Mathf.Min(SpawnRegionGeometry.WindowSizeFor(cand, ss), maxWindow);
            var win = SpawnClassificationSeam.RunClassificationDetailed(
                config, biomeTypes, cand.BBoxCenterWorld.x, cand.BBoxCenterWorld.y, windowSize);

            int w = win.Resolution, h = win.Resolution;
            int n = w * h;
            float cellAreaM2 = win.PitchX * win.PitchZ;

            var grassComps = ConnectedComponents.Label(win.WinnerBiomeIndex, w, h, v => v == grassBi);
            var forestComps = ConnectedComponents.Label(win.WinnerBiomeIndex, w, h, v => v == forestBi);
            if (grassComps.Count == 0 || forestComps.Count == 0) return null;

            // 段1↔段2の CC 同一性をオーバーラップで担保する。
            // Ensure stage1/stage2 CC identity via overlap.
            var grid = cand.SourceGrid;
            var stage1Cover = new HashSet<int>();
            foreach (int cidx in cand.Stage1GrassCells)
            {
                int ccx = cidx % grid.Width, ccy = cidx / grid.Width;
                float wx = grid.OriginX + ccx * grid.CellSize;
                float wz = grid.OriginZ + ccy * grid.CellSize;
                int cbx = Mathf.RoundToInt((wx - win.OriginX) / win.PitchX);
                int cby = Mathf.RoundToInt((wz - win.OriginZ) / win.PitchZ);
                if (cbx < 0 || cbx >= w || cby < 0 || cby >= h) continue;
                stage1Cover.Add(cby * w + cbx);
            }

            ConnectedComponents.Component gComp = null;
            int bestOverlap = 0;
            foreach (var gc in grassComps)
            {
                int overlap = 0;
                foreach (int idx in gc.Cells)
                    if (stage1Cover.Contains(idx)) overlap++;
                if (overlap > bestOverlap) { bestOverlap = overlap; gComp = gc; }
            }
            if (gComp == null || bestOverlap == 0) return null;
            if (gComp.Area * cellAreaM2 < ss.minGrasslandArea) return null;

            ConnectedComponents.Component bestForest = null;
            float bestContactM = 0f;
            foreach (var f in forestComps)
            {
                if (f.Area * cellAreaM2 < ss.minForestArea) continue;
                int contactCells = ConnectedComponents.BorderContactCells(gComp, f, win.WinnerBiomeIndex, w, h);
                float contactM = contactCells * win.PitchX;
                if (contactM > bestContactM) { bestContactM = contactM; bestForest = f; }
            }
            if (bestForest == null || bestContactM < ss.minBorderContact) return null;

            float edgeMarginM = SpawnRegionGeometry.EdgeMargin(config, ss);
            int edgeMarginCells = Mathf.CeilToInt(edgeMarginM / win.PitchX);

            var grassMask = new bool[n];
            foreach (int idx in gComp.Cells) grassMask[idx] = true;

            var grassDistCell = DistanceTransform.ChebyshevToFalse(grassMask, w, h);
            var landMaskBool = new bool[n];
            for (int i = 0; i < n; i++)
                landMaskBool[i] = win.LandMask[i] > 0.5f && win.BeachFactor[i] <= 0.2f;
            var waterDistCell = DistanceTransform.ChebyshevToFalse(landMaskBool, w, h);

            var grassClear = new float[n];
            var waterClear = new float[n];
            for (int i = 0; i < n; i++)
            {
                int x = i % w, y = i / w;
                bool inMargin = x < edgeMarginCells || x >= w - edgeMarginCells
                             || y < edgeMarginCells || y >= h - edgeMarginCells;
                if (!grassMask[i] || inMargin)
                {
                    grassClear[i] = 0f; waterClear[i] = 0f; continue;
                }
                grassClear[i] = grassDistCell[i] * win.PitchX;
                waterClear[i] = waterDistCell[i] * win.PitchX;
            }

            int best = DistanceTransform.PickPole(grassClear, waterClear, n,
                ss.grassClearanceMin, ss.waterClearanceMin);
            if (best < 0) return null;

            int bx = best % w, by = best / w;
            float sx = win.OriginX + bx * win.PitchX;
            float sz = win.OriginZ + by * win.PitchZ;
            var s = new Vector2(sx, sz);

            // 中央化: G = S - spawnTarget。
            // Centering: G = S - spawnTarget.
            Vector2 g = s - spawnTarget;

            float minClear = Mathf.Min(grassClear[best], waterClear[best]);
            float finalScore = ss.wGrasslandArea * (gComp.Area * cellAreaM2)
                             + ss.wForestArea * (bestForest.Area * cellAreaM2)
                             + ss.wBorderContact * bestContactM
                             + ss.wInland * minClear;

            sb.AppendLine($"  stage2 OK: S=({sx:F0},{sz:F0}) contact={bestContactM:F0}m minClear={minClear:F0}m");
            return (new SpawnSearchResult(true, g, s, finalScore, ""), finalScore, s);
        }
    }
}
