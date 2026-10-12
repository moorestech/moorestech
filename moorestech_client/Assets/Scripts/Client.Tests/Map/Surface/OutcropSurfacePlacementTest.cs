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
            Place(root, Grounded);
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
            Place(root, new TerrainSurfacePresentation.Legacy());
            Assert.That(root.transform.position, Is.EqualTo(position));
        }

        [Test]
        public void coreを超えるmeshはロード段で契約違反になる()
        {
            // 寸法違反はロード段のprefab単位で記録される
            // A size violation is recorded per prefab at load time
            var root = CreateMesh(0f, 5f);
            LogAssert.Expect(LogType.Error, new Regex("\\[OutcropPrefab\\] .*exceeds the grading core"));
            Assert.That(OutcropPrefab.Create(root, Grounded).ContractViolation, Does.Contain("exceeds the grading core"));
        }

        [Test]
        public void 旧worldはcoreを超えるprefabも契約違反にしない()
        {
            var root = CreateMesh(0f, 5f);
            Assert.That(OutcropPrefab.Create(root, new TerrainSurfacePresentation.Legacy()).ContractViolation, Is.Null);
        }

        [Test]
        public void terrainが無い位置は明示失敗する()
        {
            var root = CreateMesh(0f, 2f);
            var missing = new Bounds(SurfaceTerrainIsolation.ResolveTranslation(Vector2.zero), Vector3.one);
            var outcrop = OutcropPrefab.Create(root, Grounded);
            LogAssert.Expect(LogType.Error, new Regex("\\[OutcropSurfacePlacement\\] No terrain contains"));
            Assert.Throws<InvalidOperationException>(() => OutcropSurfacePlacement.Place(root, outcrop, missing, Grounded));
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
            Place(root, Grounded);
            var visible = root.transform.GetChild(0).GetComponent<Renderer>();
            Assert.That(visible.bounds.min.y - _groundY, Is.InRange(0f, 0.02f));
        }

        [Test]
        public void 非アクティブなroot描画は接地対象にならない()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(root);
            root.SetActive(false);
            LogAssert.Expect(LogType.Error, new Regex("\\[OutcropPrefab\\] No enabled MeshRenderer"));
            Assert.That(OutcropPrefab.Create(root, Grounded).ContractViolation, Does.Contain("No enabled MeshRenderer"));
        }

        [Test]
        public void SkinnedMeshRendererだけのprefabも接地する()
        {
            // modのskinned露頭もbind姿勢のAABBで寸法を測って接地する
            // A mod's skinned outcrop is sized by its bind-pose AABB and grounded
            var root = new GameObject("OutcropSkinnedFixture");
            _created.Add(root);
            var child = new GameObject("Skinned");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(1f, -3f, -1f);
            var skinned = child.AddComponent<SkinnedMeshRenderer>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(cube);
            skinned.sharedMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            skinned.localBounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));
            Place(root, Grounded);

            // localBoundsの8隅を実座標へ移し、底と中心を検査する
            // Map the eight localBounds corners to world space and check bottom and center
            var minY = float.MaxValue;
            var center = child.transform.TransformPoint(Vector3.zero);
            for (var corner = 0; corner < 8; corner++)
            {
                var point = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                minY = Mathf.Min(minY, child.transform.TransformPoint(point).y);
            }
            Assert.That(minY - _groundY, Is.InRange(0f, 0.02f));
            Assert.That(center.x, Is.EqualTo(_veinBounds.center.x).Within(0.001f));
            Assert.That(center.z, Is.EqualTo(_veinBounds.center.z).Within(0.001f));
        }

        private static TerrainSurfacePresentation Grounded => new TerrainSurfacePresentation.Grounded(SurfaceEnvelope.GeneratedV5);

        private void Place(GameObject root, TerrainSurfacePresentation presentation)
        {
            var outcrop = OutcropPrefab.Create(root, presentation);
            Assert.That(outcrop.ContractViolation, Is.Null);
            OutcropSurfacePlacement.Place(root, outcrop, _veinBounds, presentation);
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
