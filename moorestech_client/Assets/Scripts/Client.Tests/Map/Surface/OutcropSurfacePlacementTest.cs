using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.Outcrop;
using Game.MapGeneration.Facade.Surface;
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
            var terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.transform.position = new Vector3(10000f, 2f, 10000f);
            _created.Add(data);
            _created.Add(terrainObject);
            _groundY = terrainObject.GetComponent<Terrain>().SampleHeight(new Vector3(10016f, 0f, 10016f)) + 2f;
            _veinBounds = new Bounds(new Vector3(10016f, 15f, 10016f), new Vector3(5f, 6f, 5f));
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = _created.Count - 1; index >= 0; index--)
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

            // boundsだけでなく実頂点の地表交差を検査する
            // Check actual vertices for ground intersection as well as bounds
            var filter = root.GetComponentInChildren<MeshFilter>();
            foreach (var vertex in filter.sharedMesh.vertices)
                Assert.That(filter.transform.TransformPoint(vertex).y, Is.GreaterThanOrEqualTo(_groundY));
        }

        [Test]
        public void 旧worldの位置は変えない()
        {
            var root = CreateMesh(-3f, 2f);
            var position = root.transform.position;
            OutcropSurfacePlacement.Place(root, _veinBounds, new TerrainSurfacePresentation.Existing());
            Assert.That(root.transform.position, Is.EqualTo(position));
        }

        [Test]
        public void coreを超えるmeshは明示失敗する()
        {
            var root = CreateMesh(0f, 5f);
            LogAssert.Expect(LogType.Warning, new Regex("\\[OutcropSurfacePlacement\\]"));
            Assert.Throws<InvalidOperationException>(() => OutcropSurfacePlacement.Place(root, _veinBounds,
                new TerrainSurfacePresentation.Grounded(SurfaceEnvelope.GeneratedV5)));
        }

        [Test]
        public void terrainが無い位置は明示失敗する()
        {
            var root = CreateMesh(0f, 2f);
            var missing = new Bounds(new Vector3(-100000f, 0f, -100000f), Vector3.one);
            LogAssert.Expect(LogType.Warning, new Regex("\\[OutcropSurfacePlacement\\]"));
            Assert.Throws<InvalidOperationException>(() => OutcropSurfacePlacement.Place(root, missing,
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
