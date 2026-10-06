using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Map.Outcrop;
using Core.Master;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Visual;
using Newtonsoft.Json;
using NUnit.Framework;
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
                // 本番最終配列から実際のUnity地形を構築する
                // Construct actual Unity terrain from the production final arrays
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
                var core = new Rect(center.x - 2f, center.z - 2f, 4f, 4f);
                float ground = AssertFlatCore(core);
                AssertRangeBottom(vein);
                var master = MasterHolder.MapVeinMaster.GetElementOrNull(new Guid(vein.VeinGuid));
                Assert.That(master, Is.Not.Null, vein.VeinGuid);
                Assert.That(paths.ContainsKey(master.OutcropAddressablePath), Is.True, master.OutcropAddressablePath);
                var prefab = SurfaceAssetContractInputs.LoadPrefab(paths[master.OutcropAddressablePath]);
                Assert.That(prefab, Is.Not.Null, master.OutcropAddressablePath);

                // 平坦面検査後に実Prefabを本番配置処理へ渡す
                // Pass the actual prefab through production placement only after validating its flat support
                var instance = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                _created.Add(instance);
                var bounds = new Bounds(center + _translation, (Vector3)(vein.Max - vein.Min + Vector3Int.one));
                OutcropSurfacePlacement.Place(instance, bounds, new TerrainSurfacePresentation.Grounded(SurfaceEnvelope.GeneratedV5));
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

            // Unityへ適用後の全陸地支持頂点も測定する
            // Measure all land support vertices after applying them to Unity
            for (int z = 0; z < _final.Geometry.Depth; z++)
            for (int x = 0; x < _final.Geometry.Width; x++)
            {
                if (!_generated.Original.Land.IsProtectedVertex(x, z)) continue;
                float height = ActualVertex(x, z);
                if (height >= minimum) continue;
                minimum = height;
                worst = _final.Geometry.ScenePosition(x, z);
            }
            TestContext.WriteLine(JsonConvert.SerializeObject(new { actualTerrainMinimumLand = minimum, x = worst.x, z = worst.y }));
            Assert.That(minimum, Is.GreaterThanOrEqualTo(4.9f));
        }

        private float AssertFlatCore(Rect core)
        {
            var support = _final.Geometry.SupportVertices(core);
            float plane = ActualVertex(support.xMin, support.yMin);
            for (int z = support.yMin; z < support.yMax; z++)
            for (int x = support.xMin; x < support.xMax; x++)
                Assert.That(ActualVertex(x, z), Is.EqualTo(plane).Within(0.00005f), $"Non-flat core at {x},{z}");
            return plane;
        }

        private void AssertRangeBottom(PlacedVein vein)
        {
            var bottom = Rect.MinMaxRect(vein.Min.x, vein.Min.z, vein.Max.x + 1f, vein.Max.z + 1f);
            var support = _final.Geometry.SupportVertices(bottom);
            float maxGap = _generated.Run.Config.terrainHeight / SurfaceQuantization.TerrainStorageSteps + 0.00105f;

            // 範囲下端矩形の全補間支持頂点を実TerrainDataで検査する
            // Inspect all interpolation support vertices of the range bottom in actual TerrainData
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
            for (int index = _created.Count - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(_created[index]);
            _created.Clear();
        }
    }
}
