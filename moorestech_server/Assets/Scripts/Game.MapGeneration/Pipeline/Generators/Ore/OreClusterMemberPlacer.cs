using System.Collections.Generic;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Surface.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    internal static class OreClusterMemberPlacer
    {
        public static void Place(OreEntry entry, OreBand targetBand, float centerX, float centerZ,
            List<PlacementEntry> clusterMembers, float[,] heights, TerrainDimensions dims,
            System.Random rng, SpatialGrid oreGrid, TerrainSurroundEffectType surroundEffect,
            IReadOnlyList<PlacedVein> excludedVeins, VeinPlacementBatch result,
            IVeinLandConstraint landConstraint, WorldSurfaceRevision revision)
        {
            float w = dims.TerrainWidth;
            float l = dims.TerrainLength;
            int hRes = dims.Resolution;

            // 既存の有限リトライ内で陸上候補だけを採用する
            // Accept land candidates within the existing finite retry loop
            int clusterCount = rng.Next(1, targetBand.maxObjectsPerCluster + 1);
            float oreMinDist = targetBand.minDistanceBetweenOres;
            int retries = Mathf.Max(1, targetBand.placementRetries);
            for (int i = 0; i < clusterCount; i++)
            {
                float mx = 0f, mz = 0f;
                Vector3 worldPosition = default;
                PlacedVein vein = default;
                bool veinFound = false;
                for (int attempt = 0; attempt < retries; attempt++)
                {
                    float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                    float radius = (float)rng.NextDouble() * targetBand.clusterRadius;
                    mx = Mathf.Round(centerX + Mathf.Cos(angle) * radius + dims.WorldOffsetX) - dims.WorldOffsetX;
                    mz = Mathf.Round(centerZ + Mathf.Sin(angle) * radius + dims.WorldOffsetZ) - dims.WorldOffsetZ;

                    // このタイル矩形内かつワールド整数という制限が CanOverlapAnyCandidateInTile の候補範囲導出の前提。
                    // Staying inside this tile rectangle on world integers is the premise CanOverlapAnyCandidateInTile derives its candidate range from.
                    if (mx < 0 || w <= mx || mz < 0 || l <= mz) continue;
                    // AABBの排他は排他グリッドへ載せる前に判定する。落選点を先に載せると幽霊として後続候補を弾き続ける
                    // The AABB exclusion runs before anything enters the exclusion grid; a rejected point entered first would haunt later candidates
                    float my = OrePlacementMath.SampleHeight(heights, mx, mz, w, l, hRes) * dims.TerrainHeight;
                    worldPosition = new Vector3(mx + dims.WorldOffsetX, my, mz + dims.WorldOffsetZ);
                    var candidate = VeinAabbBuilder.Build(entry.veinGuid, worldPosition);
                    if (!landConstraint.Accept(candidate)) continue;
                    if (0f < oreMinDist && oreGrid.HasNeighborWithin(mx, mz, oreMinDist)) continue;
                    if (Overlaps(candidate, excludedVeins)) continue;
                    if (Overlaps(candidate, result.Veins)) continue;

                    vein = candidate;
                    veinFound = true;
                    break;
                }
                if (!veinFound) continue;

                var placement = PlacementEntry.CreateVein(entry.veinGuid, worldPosition, surroundEffect);
                clusterMembers.Add(placement);
                result.Entries.Add(placement);
                result.Veins.Add(vein);
                oreGrid.Add(mx, mz);
            }

            #region Internal

            bool Overlaps(PlacedVein candidate, IReadOnlyList<PlacedVein> veins)
            {
                // 整地で高さが揃う新版はXZだけで重複を防ぐ
                // New grading aligns heights, so prevent overlap using XZ alone
                return revision == WorldSurfaceRevision.Legacy4
                    ? VeinAabbBuilder.OverlapsAny(candidate, veins)
                    : VeinAabbBuilder.OverlapsAnyXz(candidate, veins);
            }

            #endregion
        }
    }
}
