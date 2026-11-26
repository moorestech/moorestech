using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Client.Common.PrefabPostProcess.MeshMerge
{
    /// <summary>
    /// 複数のMeshRendererを、指定した空間の座標で1つのメッシュへまとめる。マテリアルごとにサブメッシュを分ける
    /// Merges several MeshRenderers into one mesh in the given space, with one submesh per material
    /// </summary>
    public static class MeshMerger
    {
        public static bool CanMerge(MeshRenderer renderer, out string reason)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var mesh = filter == null ? null : filter.sharedMesh;
            reason = mesh == null ? "MeshFilterかメッシュが無い"
                : !mesh.isReadable ? $"メッシュ {mesh.name} が読み取り不可(Read/Writeオフ)"
                : renderer.sharedMaterials.Length != mesh.subMeshCount ? $"マテリアル数{renderer.sharedMaterials.Length}とサブメッシュ数{mesh.subMeshCount}が違う"
                : HasNonTriangleSubMesh(mesh) ? $"メッシュ {mesh.name} に三角形以外のサブメッシュがある"
                : null;
            return reason == null;
        }

        public static MeshMergeResult Merge(Transform space, IReadOnlyList<MeshRenderer> sources, IMergedVertexAttributeWriter writer)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uv0 = new List<Vector2>();
            var channels = writer.GetUvChannels();
            var extraUvs = new List<Vector4>[channels.Count];
            for (var i = 0; i < extraUvs.Length; i++) extraUvs[i] = new List<Vector4>();
            var materials = new List<Material>();
            var indicesByMaterial = new List<List<int>>();

            foreach (var source in sources) AppendSource(source);
            return new MeshMergeResult(BuildMesh(), materials.ToArray());

            #region Internal

            void AppendSource(MeshRenderer source)
            {
                var mesh = source.GetComponent<MeshFilter>().sharedMesh;
                var toSpace = space.worldToLocalMatrix * source.transform.localToWorldMatrix;
                var normalMatrix = toSpace.inverse.transpose;
                // 負の大きさを焼き込むと面の表裏が反転するので、頂点を結ぶ順も反転する
                // Baking a negative scale flips faces, so the winding is reversed as well
                var flipped = toSpace.determinant < 0f;
                var baseVertex = positions.Count;

                AppendVertices(mesh, toSpace, normalMatrix, flipped);
                for (var c = 0; c < channels.Count; c++)
                {
                    var value = writer.GetSourceValue(channels[c], source);
                    for (var v = 0; v < mesh.vertexCount; v++) extraUvs[c].Add(value);
                }

                var sharedMaterials = source.sharedMaterials;
                for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    var indices = GetIndices(sharedMaterials[subMesh]);
                    var triangles = mesh.GetTriangles(subMesh);
                    for (var t = 0; t < triangles.Length; t += 3)
                    {
                        indices.Add(baseVertex + triangles[t]);
                        indices.Add(baseVertex + triangles[flipped ? t + 2 : t + 1]);
                        indices.Add(baseVertex + triangles[flipped ? t + 1 : t + 2]);
                    }
                }
            }

            void AppendVertices(Mesh mesh, Matrix4x4 toSpace, Matrix4x4 normalMatrix, bool flipped)
            {
                var srcPositions = mesh.vertices;
                var srcNormals = mesh.normals;
                var srcTangents = mesh.tangents;
                var srcUv0 = mesh.uv;
                for (var v = 0; v < srcPositions.Length; v++)
                {
                    positions.Add(toSpace.MultiplyPoint3x4(srcPositions[v]));
                    normals.Add(srcNormals.Length > 0 ? normalMatrix.MultiplyVector(srcNormals[v]).normalized : Vector3.up);
                    var tangent = srcTangents.Length > 0 ? srcTangents[v] : new Vector4(1f, 0f, 0f, 1f);
                    var tangentDirection = toSpace.MultiplyVector(tangent).normalized;
                    tangents.Add(new Vector4(tangentDirection.x, tangentDirection.y, tangentDirection.z, flipped ? -tangent.w : tangent.w));
                    uv0.Add(srcUv0.Length > 0 ? srcUv0[v] : Vector2.zero);
                }
            }

            List<int> GetIndices(Material material)
            {
                var index = materials.IndexOf(material);
                if (index >= 0) return indicesByMaterial[index];
                materials.Add(material);
                indicesByMaterial.Add(new List<int>());
                return indicesByMaterial[^1];
            }

            Mesh BuildMesh()
            {
                var mesh = new Mesh { name = space.name + " (Merged)" };
                if (positions.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetTangents(tangents);
                mesh.SetUVs(0, uv0);
                for (var c = 0; c < channels.Count; c++) mesh.SetUVs(channels[c], extraUvs[c]);
                mesh.subMeshCount = indicesByMaterial.Count;
                for (var i = 0; i < indicesByMaterial.Count; i++) mesh.SetTriangles(indicesByMaterial[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }

            #endregion
        }

        private static bool HasNonTriangleSubMesh(Mesh mesh)
        {
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) != MeshTopology.Triangles) return true;
            }
            return false;
        }
    }
}
