using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.Outcrop;
using Game.MapGeneration.Surface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace Client.Tests.Map.Surface
{
    public class OutcropSurfacePlacementTest
    {
        private readonly List<UnityEngine.Object> _created = new();
        private Bounds _veinBounds;
        private float _groundY;

        [SetUp]
        public void SetUp()
        {
            // 実Terrainの補間面を平坦に固定する
            // Fix the actual interpolated Terrain surface to a flat plane
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 20f, 32f) };
            var heights = new float[33, 33];
            for (var z = 0; z < 33; z++)
                for (var x = 0; x < 33; x++) heights[z, x] = 0.5f;
            data.SetHeights(0, 0, heights);
            var translation = SurfaceTerrainIsolation.ResolveTranslation(Vector2.zero);
            var terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.transform.position = translation + Vector3.up * 2f;
            _created.Add(data);
            _created.Add(terrainObject);
            _groundY = terrainObject.GetComponent<Terrain>().SampleHeight(translation + new Vector3(16f, 0f, 16f)) + 2f;
            _veinBounds = new Bounds(translation + new Vector3(16f, 15f, 16f), new Vector3(5f, 6f, 5f));
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = _created.Count - 1; 0 <= index; index--)
                UnityEngine.Object.DestroyImmediate(_created[index]);
            _created.Clear();
        }

        [TestCase(-3f)]
        [TestCase(3f)]
        public void 作者pivotが正負でも全頂点を平坦面へ接地する(float pivot)
        {
            var root = CreateMesh(pivot, 2f);
            OutcropSurfacePlacement.Place(root, _veinBounds, new TerrainSurfacePresentation.Grounded(SurfaceEnvelope.GeneratedV5));
            var renderer = root.GetComponentInChildren<Renderer>();
            Assert.That(renderer.bounds.center.x, Is.EqualTo(_veinBounds.center.x).Within(0.001f));
            Assert.That(renderer.bounds.center.z, Is.EqualTo(_veinBounds.center.z).Within(0.001f));
            Assert.That(renderer.bounds.min.y - _groundY, Is.InRange(0f, 0.02f));

            // boundsでなく実頂点の交差を検査
            // Check real vertices for ground intersection, not bounds
            var filter = root.GetComponentInChildren<MeshFilter>();
            foreach (var vertex in filter.sharedMesh.vertices)
                Assert.That(filter.transform.TransformPoint(vertex).y, Is.GreaterThanOrEqualTo(_groundY));
        }

        [Test]
        public void 旧worldの位置は変えない()
        {
            var root = CreateMesh(-3f, 2f);
            var position = root.transform.position;
            OutcropSurfacePlacement.Place(root, _veinBounds, new TerrainSurfacePresentation.Legacy());
            Assert.That(root.transform.position, Is.EqualTo(position));
        }

        [Test]
        public void coreを超えるmeshは明示失敗する()
        {
            var root = CreateMesh(0f, 5f);
            LogAssert.Expect(LogType.Error, new Regex("\\[OutcropSurfacePlacement\\]"));
            Assert.Throws<InvalidOperationException>(() => OutcropSurfacePlacement.Place(root, _veinBounds,
                new TerrainSurfacePresentation.Grounded(SurfaceEnvelope.GeneratedV5)));
        }

        [Test]
        public void terrainが無い位置は明示失敗する()
        {
            var root = CreateMesh(0f, 2f);
            var missing = new Bounds(SurfaceTerrainIsolation.ResolveTranslation(Vector2.zero), Vector3.one);
            LogAssert.Expect(LogType.Error, new Regex("\\[OutcropSurfacePlacement\\]"));
            Assert.Throws<InvalidOperationException>(() => OutcropSurfacePlacement.Place(root, missing,
                new TerrainSurfacePresentation.Grounded(SurfaceEnvelope.GeneratedV5)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 非描画rendererは接地範囲から除外する(bool disabledComponent)
        {
            var root = CreateMesh(0f, 2f);
            var hidden = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hidden.transform.SetParent(root.transform, false);
            hidden.transform.localScale = Vector3.one * 100f;
            hidden.transform.localPosition = Vector3.down * 50f;
            if (disabledComponent) hidden.GetComponent<Renderer>().enabled = false;
            else hidden.SetActive(false);

            // 非描画の巨大meshは底面と寸法に無関係
            // A large invisible mesh must not affect bottom or size
            OutcropSurfacePlacement.Place(root, _veinBounds, new TerrainSurfacePresentation.Grounded(SurfaceEnvelope.GeneratedV5));
            var visible = root.transform.GetChild(0).GetComponent<Renderer>();
            Assert.That(visible.bounds.min.y - _groundY, Is.InRange(0f, 0.02f));
        }

        [Test]
        public void 非アクティブなroot描画は接地対象にならない()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(root);
            root.SetActive(false);
            LogAssert.Expect(LogType.Error, new Regex("\\[OutcropSurfacePlacement\\]"));
            Assert.Throws<InvalidOperationException>(() => OutcropSurfacePlacement.Place(root, _veinBounds,
                new TerrainSurfacePresentation.Grounded(SurfaceEnvelope.GeneratedV5)));
        }

        private GameObject CreateMesh(float pivot, float width)
        {
            var root = new GameObject("OutcropPivotFixture");
            _created.Add(root);
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(1f, pivot, -1f);
            child.transform.localScale = new Vector3(width, 2f, 2f);
            return root;
        }
    }
}
