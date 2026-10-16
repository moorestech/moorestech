using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Game.InGame.Map.Outcrop;
using Game.MapGeneration.Surface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Map.Surface
{
    // 露頭はワールド生成が建てた地形だけへ接地し、シーン上の無関係な地形を見ないことを検査する
    // Verifies outcrops ground only on terrain built by world generation and never consult unrelated scene terrain
    public class OutcropOwnedTerrainCandidateTest
    {
        private readonly List<UnityEngine.Object> _created = new();
        private Vector3 _translation;
        private Bounds _veinBounds;

        private static TerrainSurfacePresentation Grounded => new TerrainSurfacePresentation.Grounded(SurfaceEnvelope.GeneratedV5);

        [SetUp]
        public void SetUp()
        {
            _translation = SurfaceTerrainIsolation.ResolveTranslation(Vector2.zero);
            _veinBounds = new Bounds(_translation + new Vector3(16f, 15f, 16f), new Vector3(5f, 6f, 5f));
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = _created.Count - 1; 0 <= index; index--)
                UnityEngine.Object.DestroyImmediate(_created[index]);
            _created.Clear();
        }

        [Test]
        public void 冒頭スキットで非表示の環境配下の地形にも接地する()
        {
            // 本番と同じく非表示を含めて環境配下から候補を集める
            // Collect candidates under the hidden environment including inactive ones, as production does
            var environment = new GameObject("HiddenEnvironmentFixture");
            _created.Add(environment);
            var owned = CreateTerrain(_translation + Vector3.up * 2f, 0.5f);
            owned.transform.SetParent(environment.transform);
            environment.SetActive(false);
            Assert.That(Array.IndexOf(Terrain.activeTerrains, owned), Is.EqualTo(-1));
            var root = PlaceCube(environment.GetComponentsInChildren<Terrain>(true));
            var groundY = owned.SampleHeight(_veinBounds.center) + owned.transform.position.y;
            Assert.That(root.GetComponent<Renderer>().bounds.min.y - groundY, Is.InRange(0f, 0.02f));
        }

        [Test]
        public void 候補外の重なる地形は原点が小さくても接地に使わない()
        {
            // 辞書順なら勝つ低い地形を候補外に置く
            // Place a lower terrain that would win the lexicographic order outside the candidates
            CreateTerrain(_translation + Vector3.left * 4f, 0f);
            var owned = CreateTerrain(_translation + Vector3.up * 2f, 0.5f);
            var root = PlaceCube(new[] { owned });
            var groundY = owned.SampleHeight(_veinBounds.center) + owned.transform.position.y;
            Assert.That(root.GetComponent<Renderer>().bounds.min.y - groundY, Is.InRange(0f, 0.02f));
        }

        [Test]
        public void 候補外にTerrainData未設定の地形があっても接地する()
        {
            // 無関係な未設定地形はシーンにあっても起動を止めない
            // An unrelated terrain without data must not stop startup
            var unrelated = new GameObject("UnrelatedTerrainWithoutData");
            _created.Add(unrelated);
            unrelated.AddComponent<Terrain>();
            var owned = CreateTerrain(_translation + Vector3.up * 2f, 0.5f);
            var root = PlaceCube(new[] { owned });
            var groundY = owned.SampleHeight(_veinBounds.center) + owned.transform.position.y;
            Assert.That(root.GetComponent<Renderer>().bounds.min.y - groundY, Is.InRange(0f, 0.02f));
        }

        [Test]
        public void 生成地形のTerrainData欠落は地形名付きで明示失敗する()
        {
            var broken = new GameObject("GeneratedTerrainWithoutData");
            _created.Add(broken);
            var terrain = broken.AddComponent<Terrain>();
            LogAssert.Expect(LogType.Error, new Regex("\\[OutcropSurfacePlacement\\] Terrain 'GeneratedTerrainWithoutData' has no TerrainData"));
            Assert.Throws<InvalidOperationException>(() => PlaceCube(new[] { terrain }));
        }

        private GameObject PlaceCube(Terrain[] ownedTerrains)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(root);
            var outcrop = OutcropPrefab.Create(root, Grounded);
            Assert.That(outcrop.ContractViolation, Is.Null);
            OutcropSurfacePlacement.Place(root, outcrop, _veinBounds, Grounded, ownedTerrains);
            return root;
        }

        private Terrain CreateTerrain(Vector3 position, float height)
        {
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 20f, 32f) };
            _created.Add(data);
            var heights = new float[33, 33];
            for (var z = 0; z < 33; z++)
                for (var x = 0; x < 33; x++) heights[z, x] = height;
            data.SetHeights(0, 0, heights);
            var instance = Terrain.CreateTerrainGameObject(data);
            _created.Add(instance);
            instance.transform.position = position;
            return instance.GetComponent<Terrain>();
        }
    }
}
