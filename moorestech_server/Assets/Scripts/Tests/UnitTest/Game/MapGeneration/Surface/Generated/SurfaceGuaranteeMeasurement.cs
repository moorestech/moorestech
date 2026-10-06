using System.Linq;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Surface;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated
{
    public sealed class SurfaceGuaranteeMeasurement
    {
        public int LandVertices;
        public int Veins;
        public int OriginalLowSeaVertices;
        public int RemainingLowSeaVertices;
        public int OffLandFootprints;
        public int NonFlatCoreVertices;
        public int BuriedRangeVertices;
        public int ExcessRangeGaps;
        public float MinimumLand = float.PositiveInfinity;
        public Vector2 MinimumLandPosition;
        public float MinimumRangeGap = float.PositiveInfinity;
        public Vector2 MinimumRangePosition;

        public static SurfaceGuaranteeMeasurement Measure(GeneratedSurfaceFixture fixture, SurfaceTileGrid final)
        {
            var result = new SurfaceGuaranteeMeasurement();
            float terrainHeight = fixture.Run.Config.terrainHeight;
            float quantum = terrainHeight / SurfaceQuantization.TerrainStorageSteps;

            // 陸セルの支持頂点を全域検査
            // Inspect support vertices of all land cells
            for (int z = 0; z < final.Geometry.Depth; z++)
            for (int x = 0; x < final.Geometry.Width; x++)
            {
                if (!fixture.Original.Land.IsProtectedVertex(x, z))
                {
                    // 内海全域の持上げ検出に低い海底を数える
                    // Count low seabed vertices to detect blanket raising of the sea
                    if (fixture.Original.GetHeight(x, z) < SurfaceGuaranteeBounds.SeaY) result.OriginalLowSeaVertices++;
                    if (final.GetHeight(x, z) < SurfaceGuaranteeBounds.SeaY) result.RemainingLowSeaVertices++;
                    continue;
                }
                result.LandVertices++;
                float height = final.GetHeight(x, z);
                if (result.MinimumLand <= height) continue;
                result.MinimumLand = height;
                result.MinimumLandPosition = final.Geometry.ScenePosition(x, z);
            }

            var veins = fixture.Run.Output.ItemVeins.Concat(fixture.Run.Output.FluidVeins);
            result.Veins = fixture.Run.Output.ItemVeins.Count + fixture.Run.Output.FluidVeins.Count;
            Assert.That(fixture.Run.Ledger.GroundingPads.Count, Is.EqualTo(result.Veins));
            foreach (var pad in fixture.Run.Ledger.GroundingPads)
            {
                var outer = Rect.MinMaxRect(pad.Core.xMin - pad.BlendWidth, pad.Core.yMin - pad.BlendWidth,
                    pad.Core.xMax + pad.BlendWidth, pad.Core.yMax + pad.BlendWidth);
                if (!fixture.Original.Land.ContainsSupport(outer)) result.OffLandFootprints++;
                var core = final.Geometry.SupportVertices(pad.Core);

                // core支持点が同一高さでなければ失敗
                // Fail unless the core support vertices share one height
                for (int z = core.yMin; z < core.yMax; z++)
                for (int x = core.xMin; x < core.xMax; x++)
                    if (SurfaceGuaranteeBounds.CoreFlatTolerance < Mathf.Abs(final.GetHeight(x, z) - pad.HeightMeters)) result.NonFlatCoreVertices++;
            }

            foreach (var vein in veins)
            {
                var bottom = Rect.MinMaxRect(vein.Min.x, vein.Min.z, vein.Max.x + 1f, vein.Max.z + 1f);
                var support = final.Geometry.SupportVertices(bottom);

                // AABB下端矩形の補間支持点を測る
                // Measure interpolation support vertices of the AABB bottom rectangle
                for (int z = support.yMin; z < support.yMax; z++)
                for (int x = support.xMin; x < support.xMax; x++)
                {
                    float gap = vein.Min.y - final.GetHeight(x, z);
                    if (gap < 0f) result.BuriedRangeVertices++;
                    if (quantum + SurfaceGuaranteeBounds.RangeGapTolerance < gap) result.ExcessRangeGaps++;
                    if (result.MinimumRangeGap <= gap) continue;
                    result.MinimumRangeGap = gap;
                    result.MinimumRangePosition = final.Geometry.ScenePosition(x, z);
                }
            }
            return result;
        }

        public void AssertValid()
        {
            // ゼロ件で保証を空虚に通さない
            // Prevent vacuous success from worlds with no land or no veins
            TestContext.WriteLine(JsonConvert.SerializeObject(new
            {
                LandVertices, Veins, OriginalLowSeaVertices, RemainingLowSeaVertices,
                OffLandFootprints, NonFlatCoreVertices, BuriedRangeVertices, ExcessRangeGaps,
                MinimumLand, landX = MinimumLandPosition.x, landZ = MinimumLandPosition.y,
                MinimumRangeGap, rangeX = MinimumRangePosition.x, rangeZ = MinimumRangePosition.y,
            }));
            Assert.That(LandVertices, Is.GreaterThan(0));
            Assert.That(Veins, Is.GreaterThan(0));
            Assert.That(MinimumLand, Is.GreaterThanOrEqualTo(SurfaceGuaranteeBounds.LandMinimum));
            if (0 < OriginalLowSeaVertices) Assert.That(RemainingLowSeaVertices, Is.GreaterThan(0));
            Assert.That(OffLandFootprints, Is.Zero);
            Assert.That(NonFlatCoreVertices, Is.Zero);
            Assert.That(BuriedRangeVertices, Is.Zero);
            Assert.That(ExcessRangeGaps, Is.Zero);
        }
    }
}
