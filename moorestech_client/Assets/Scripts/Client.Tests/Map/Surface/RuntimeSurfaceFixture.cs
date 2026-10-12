using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Map.Outcrop;
using Core.Master;
using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Visual;
using Newtonsoft.Json;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Surface;
using Tests.UnitTest.Game.MapGeneration.Surface.Generated;
using UnityEngine;

namespace Client.Tests.Map.Surface
{
    internal sealed class RuntimeSurfaceFixture : IDisposable
    {
        private readonly GeneratedSurfaceFixture _generated;
        private readonly SurfaceTileGrid _final;
        private readonly Vector3 _translation;
        private readonly Dictionary<Vector2Int, Terrain> _terrains = new();
        private readonly List<UnityEngine.Object> _created = new();

        internal RuntimeSurfaceFixture(GeneratedSurfaceFixture generated, IReadOnlyList<TileVisualBakeResult> baked)
        {
            _generated = generated;
            _final = generated.Grid(baked);
            _translation = SurfaceTerrainIsolation.ResolveTranslation(_final.Geometry.Origin);
            var config = generated.Run.Config;
            for (int index = 0; index < baked.Count; index++)
            {
                // 本番最終配列から実地形を構築
                // Build real terrain from the production final arrays
                var tile = generated.Run.Output.Tiles[index];
                var data = new TerrainData
                {
                    heightmapResolution = config.Resolution,
                    size = new Vector3(config.terrainWidth, config.terrainHeight, config.terrainLength),
                };
                data.SetHeights(0, 0, baked[index].DisplayHeights);
                var instance = Terrain.CreateTerrainGameObject(data);
                instance.transform.position = baked[index].ScenePosition + _translation;
                _created.Add(data);
                _created.Add(instance);
                _terrains.Add(new Vector2Int(tile.TileX, tile.TileZ), instance.GetComponent<Terrain>());
            }
        }

        internal void AssertEveryVein()
        {
            SurfaceGuaranteeMeasurement.Measure(_generated, _final).AssertValid();
            AssertActualLandVertices();
            var paths = SurfaceAssetContractInputs.CollectAddressablePaths();
            var vertices = new RuntimeMeshMeasurement();
            var veins = _generated.Run.Output.ItemVeins.Concat(_generated.Run.Output.FluidVeins).ToArray();
            foreach (var vein in veins)
            {
                var center = (Vector3)(vein.Min + vein.Max + Vector3Int.one) * 0.5f;
                float coreHalfSize = SurfaceEnvelope.GeneratedV5.CoreHalfSize;
                var core = new Rect(center.x - coreHalfSize, center.z - coreHalfSize, 2f * coreHalfSize, 2f * coreHalfSize);
                float ground = AssertFlatCore(core);
                AssertRangeBottom(vein);
                var master = MasterHolder.MapVeinMaster.GetElementOrNull(new Guid(vein.VeinGuid));
                Assert.That(master, Is.Not.Null, vein.VeinGuid);
                Assert.That(paths.ContainsKey(master.OutcropAddressablePath), Is.True, master.OutcropAddressablePath);
                var prefab = SurfaceAssetContractInputs.LoadPrefab(paths[master.OutcropAddressablePath]);
                Assert.That(prefab, Is.Not.Null, master.OutcropAddressablePath);

                // 平坦面検査後に実Prefabを配置へ渡す
                // Pass the real prefab to placement after the flat check
                var grounded = new TerrainSurfacePresentation.Grounded(SurfaceEnvelope.GeneratedV5);
                var outcrop = OutcropPrefab.Create(prefab, grounded);
                Assert.That(outcrop.ContractViolation, Is.Null, master.OutcropAddressablePath);
                var instance = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                _created.Add(instance);
                var bounds = new Bounds(center + _translation, (Vector3)(vein.Max - vein.Min + Vector3Int.one));
                OutcropSurfacePlacement.Place(instance, outcrop, bounds, grounded);
                var verifiedWorldCore = new Rect(core.position + new Vector2(_translation.x, _translation.z), core.size);
                vertices.Measure(instance, ground, vein.VeinGuid, verifiedWorldCore);
                UnityEngine.Object.DestroyImmediate(instance);
                _created.Remove(instance);
            }
            vertices.WriteJson(_translation);
            vertices.AssertValid();
        }

        private void AssertActualLandVertices()
        {
            float minimum = float.PositiveInfinity;
            Vector2 worst = Vector2.zero;

            // 適用後の陸地支持頂点も測定
            // Also measure land support vertices after applying to Unity
            for (int z = 0; z < _final.Geometry.Depth; z++)
            for (int x = 0; x < _final.Geometry.Width; x++)
            {
                if (!_generated.Original.Land.IsProtectedVertex(x, z)) continue;
                float height = ActualVertex(x, z);
                if (minimum <= height) continue;
                minimum = height;
                worst = _final.Geometry.ScenePosition(x, z);
            }
            TestContext.WriteLine(JsonConvert.SerializeObject(new { actualTerrainMinimumLand = minimum, x = worst.x, z = worst.y }));
            Assert.That(minimum, Is.GreaterThanOrEqualTo(SurfaceGuaranteeBounds.LandMinimum));
        }

        private float AssertFlatCore(Rect core)
        {
            var support = _final.Geometry.SupportVertices(core);
            float plane = ActualVertex(support.xMin, support.yMin);
            for (int z = support.yMin; z < support.yMax; z++)
            for (int x = support.xMin; x < support.xMax; x++)
                Assert.That(ActualVertex(x, z), Is.EqualTo(plane).Within(SurfaceGuaranteeBounds.CoreFlatTolerance), $"Non-flat core at {x},{z}");
            return plane;
        }

        private void AssertRangeBottom(PlacedVein vein)
        {
            var bottom = Rect.MinMaxRect(vein.Min.x, vein.Min.z, vein.Max.x + 1f, vein.Max.z + 1f);
            var support = _final.Geometry.SupportVertices(bottom);
            float maxGap = _generated.Run.Config.terrainHeight / TerrainHeightStorage.Steps + SurfaceGuaranteeBounds.RangeGapTolerance;

            // 下端矩形の補間支持頂点を検査
            // Inspect the interpolation support vertices of the bottom rectangle
            for (int z = support.yMin; z < support.yMax; z++)
            for (int x = support.xMin; x < support.xMax; x++)
                Assert.That(vein.Min.y - ActualVertex(x, z), Is.InRange(0f, maxGap), $"Range at {x},{z}");
        }

        private float ActualVertex(int x, int z)
        {
            int stride = _generated.Run.Config.Resolution - 1;
            int tileX = Mathf.Max(0, (x - 1) / stride);
            int tileZ = Mathf.Max(0, (z - 1) / stride);
            var terrain = _terrains[new Vector2Int(tileX, tileZ)];
            return terrain.terrainData.GetHeight(x - tileX * stride, z - tileZ * stride) + terrain.transform.position.y;
        }

        public void Dispose()
        {
            for (int index = _created.Count - 1; 0 <= index; index--)
                UnityEngine.Object.DestroyImmediate(_created[index]);
            _created.Clear();
        }
    }
}
