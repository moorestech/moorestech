using System.Collections.Generic;
using Client.Common.PrefabPostProcess.MeshMerge;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PrefabPostProcess
{
    public class MeshMergerTest
    {
        private readonly List<Object> _owned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _owned) Object.DestroyImmediate(obj);
            _owned.Clear();
        }

        [Test]
        public void 指定した空間の座標でまとめマテリアルごとにサブメッシュを分ける()
        {
            var space = Own(new GameObject("space")).transform;
            space.position = new Vector3(10f, 0f, 0f);
            var materialA = Own(new Material(Shader.Find("Hidden/InternalErrorShader")));
            var materialB = Own(new Material(Shader.Find("Hidden/InternalErrorShader")));
            var cubeA = CreateCube(space, new Vector3(2f, 0f, 0f), Vector3.one, materialA);
            var cubeB = CreateCube(space, new Vector3(-2f, 0f, 0f), Vector3.one, materialB);
            var cubeA2 = CreateCube(space, new Vector3(0f, 3f, 0f), Vector3.one * 2f, materialA);

            var result = MeshMerger.Merge(space, new[] { cubeA, cubeB, cubeA2 }, new ConstantWriter());
            Own(result.Mesh);

            Assert.AreEqual(2, result.Mesh.subMeshCount);
            CollectionAssert.AreEqual(new[] { materialA, materialB }, result.Materials);
            // 3つの立方体(各24頂点)が空間のローカル座標で入る。大きさ2の立方体は上端がy=4
            // Three cubes (24 vertices each) land in the space's local coordinates; the size-2 cube tops out at y=4
            Assert.AreEqual(72, result.Mesh.vertexCount);
            Assert.AreEqual(4f, result.Mesh.bounds.max.y, 1e-4f);
            Assert.AreEqual(-2.5f, result.Mesh.bounds.min.x, 1e-4f);
            Assert.AreEqual(2.5f, result.Mesh.bounds.max.x, 1e-4f);
        }

        [Test]
        public void 書き手の値が元Rendererの全頂点のUVに入る()
        {
            var space = Own(new GameObject("space")).transform;
            var material = Own(new Material(Shader.Find("Hidden/InternalErrorShader")));
            var cube = CreateCube(space, Vector3.zero, Vector3.one, material);

            var result = MeshMerger.Merge(space, new[] { cube }, new ConstantWriter());
            Own(result.Mesh);

            var uvs = new List<Vector4>();
            result.Mesh.GetUVs(ConstantWriter.Channel, uvs);
            Assert.AreEqual(result.Mesh.vertexCount, uvs.Count);
            foreach (var uv in uvs) Assert.AreEqual(ConstantWriter.Value, uv);
        }

        [Test]
        public void 負の大きさを焼き込んでも面が外を向く()
        {
            var space = Own(new GameObject("space")).transform;
            var material = Own(new Material(Shader.Find("Hidden/InternalErrorShader")));
            var cube = CreateCube(space, Vector3.zero, new Vector3(1f, -1f, 1f), material);

            var result = MeshMerger.Merge(space, new[] { cube }, new ConstantWriter());
            Own(result.Mesh);

            // 三角形の巻き方向から出した面の向きが、中心から外へ向いていること
            // The face direction derived from the winding must point away from the center
            var vertices = result.Mesh.vertices;
            var triangles = result.Mesh.triangles;
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var a = vertices[triangles[t]];
                var b = vertices[triangles[t + 1]];
                var c = vertices[triangles[t + 2]];
                var faceNormal = Vector3.Cross(b - a, c - a);
                Assert.Greater(Vector3.Dot(faceNormal, (a + b + c) / 3f), 0f);
            }
        }

        private MeshRenderer CreateCube(Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var cube = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;
            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private T Own<T>(T obj) where T : Object
        {
            _owned.Add(obj);
            return obj;
        }

        private sealed class ConstantWriter : IMergedVertexAttributeWriter
        {
            public const int Channel = 6;
            public static readonly Vector4 Value = new(1f, 2f, 3f, 4f);
            public IReadOnlyList<int> GetUvChannels() { return new[] { Channel }; }
            public Vector4 GetSourceValue(int uvChannel, MeshRenderer source) { return Value; }
        }
    }
}
