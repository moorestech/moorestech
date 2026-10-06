using Tests.Module.TestMod;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.Map.Outcrop;
using Core.Master;
using Game.MapGeneration.Surface;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Map.Surface
{
    [Category("IgnoreCI")]
    public class OutcropSurfaceAssetContractTest
    {
        private readonly List<UnityEngine.Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            // 本番マスタを標準入力へ戻す
            // Restore standard inputs so production masters do not leak
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(
                new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")))));

            for (var index = _created.Count - 1; 0 <= index; index--)
                UnityEngine.Object.DestroyImmediate(_created[index]);
            _created.Clear();
        }

        [Test]
        public void 本番マスタ全露頭の寸法と全頂点接地を検査する()
        {
            var repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var golden = JObject.Parse(File.ReadAllText(Path.Combine(repository,
                "moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Surface/Fixtures/legacy-v4-seed196.json")));
            var serverData = Path.GetFullPath(Path.Combine(repository, (string)golden["meta"]["serverDataDirectory"]));
            Assert.That(Directory.Exists(Path.Combine(serverData, "mods")), Is.True, "Pinned production master is required");
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(serverData, "mods")))));

            // 参照アドレスを全走査し欠損も失敗
            // Scan all addresses and fail on missing assets
            var paths = SurfaceAssetContractInputs.CollectAddressablePaths();
            Assert.That(MasterHolder.MapVeinMaster.All.Count, Is.GreaterThan(0));
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 20f, 32f) };
            var heights = new float[33, 33];
            for (var z = 0; z < 33; z++)
                for (var x = 0; x < 33; x++) heights[z, x] = 0.5f;
            data.SetHeights(0, 0, heights);
            var translation = SurfaceTerrainIsolation.ResolveTranslation(Vector2.zero);
            var terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.transform.position = translation;
            _created.Add(data);
            _created.Add(terrainObject);
            var bounds = new Bounds(translation + new Vector3(16f, 11f, 16f), new Vector3(5f, 2f, 5f));

            foreach (var element in MasterHolder.MapVeinMaster.All)
            {
                Assert.That(paths.ContainsKey(element.OutcropAddressablePath), Is.True, element.OutcropAddressablePath);
                var prefab = SurfaceAssetContractInputs.LoadPrefab(paths[element.OutcropAddressablePath]);
                Assert.That(prefab, Is.Not.Null, element.OutcropAddressablePath);
                var instance = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                _created.Add(instance);
                OutcropSurfacePlacement.Place(instance, bounds, new TerrainSurfacePresentation.Grounded(SurfaceEnvelope.GeneratedV5));

                // 実meshの全頂点を平坦面と比較する
                // Compare every actual mesh vertex with the flat surface
                var minimumY = float.PositiveInfinity;
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<Renderer>();
                    if (renderer == null || !renderer.enabled) continue;
                    Assert.That(filter.sharedMesh, Is.Not.Null, element.OutcropAddressablePath);
                    foreach (var vertex in filter.sharedMesh.vertices)
                        minimumY = Mathf.Min(minimumY, filter.transform.TransformPoint(vertex).y);
                }
                Assert.That(minimumY - 10f, Is.InRange(0f, 0.02f), element.OutcropAddressablePath);
            }
        }

        [Test]
        public void 水shaderの包絡式と実plane入力を検査する()
        {
            SurfaceAssetContractInputs.AssertWaterInputs();
        }
    }
}
