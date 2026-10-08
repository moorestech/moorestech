using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Map.Surface
{
    internal sealed class RuntimeMeshMeasurement
    {
        public int MeshVertices;
        public int Outcrops;
        public int BuriedVertices;
        public int OutsideCoreVertices;
        public int InvalidContactGaps;
        public float MinimumGap = float.PositiveInfinity;
        public Vector3 WorstPosition;
        public float WorstTerrainY;
        public string WorstGuid;
        public float LargestContactGap;

        internal void Measure(GameObject instance, float plane, string guid, Rect verifiedCore)
        {
            Outcrops++;
            float bottom = float.PositiveInfinity;
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0), guid);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

            // 検証済みcore中心への配置を検査
            // Check placement at the verified core center
            Assert.That(bounds.center.x, Is.EqualTo(verifiedCore.center.x).Within(0.001f), guid);
            Assert.That(bounds.center.z, Is.EqualTo(verifiedCore.center.y).Within(0.001f), guid);
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                Assert.That(filter.sharedMesh, Is.Not.Null, guid);
                var mesh = filter.sharedMesh;
                Assert.That(mesh.triangles.Length, Is.GreaterThan(0), guid);

                // 平坦core内なら全頂点で交差を否定
                // On a flat core all vertex heights rule out intersection
                foreach (var vertex in mesh.vertices)
                {
                    var world = filter.transform.TransformPoint(vertex);
                    MeshVertices++;
                    if (world.x < verifiedCore.xMin - 0.00001f || verifiedCore.xMax + 0.00001f < world.x ||
                        world.z < verifiedCore.yMin - 0.00001f || verifiedCore.yMax + 0.00001f < world.z)
                        OutsideCoreVertices++;
                    float gap = world.y - plane;
                    bottom = Mathf.Min(bottom, gap);
                    if (gap < 0f) BuriedVertices++;
                    if (MinimumGap <= gap) continue;
                    MinimumGap = gap;
                    WorstPosition = world;
                    WorstTerrainY = plane;
                    WorstGuid = guid;
                }
            }
            if (bottom < 0f || 0.02f < bottom) InvalidContactGaps++;
            LargestContactGap = Mathf.Max(LargestContactGap, bottom);
        }

        internal void WriteJson(Vector3 translation)
        {
            TestContext.WriteLine(JsonConvert.SerializeObject(new
            {
                MeshVertices, Outcrops, BuriedVertices, OutsideCoreVertices, InvalidContactGaps, MinimumGap, LargestContactGap,
                vertexX = WorstPosition.x, vertexY = WorstPosition.y, vertexZ = WorstPosition.z,
                WorstTerrainY, WorstGuid, translationX = translation.x, translationZ = translation.z,
                originalVertexX = WorstPosition.x - translation.x, originalVertexZ = WorstPosition.z - translation.z,
            }));
        }

        internal void AssertValid()
        {
            // meshの欠損をゼロ交差と誤認しない
            // Do not mistake missing meshes for zero intersections
            Assert.That(Outcrops, Is.GreaterThan(0));
            Assert.That(MeshVertices, Is.GreaterThan(0));
            Assert.That(BuriedVertices, Is.Zero);
            Assert.That(OutsideCoreVertices, Is.Zero);
            Assert.That(InvalidContactGaps, Is.Zero);
            Assert.That(MinimumGap, Is.GreaterThanOrEqualTo(0f));
            Assert.That(LargestContactGap, Is.LessThanOrEqualTo(0.02f));
        }
    }
}
