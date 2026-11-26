using UnityEngine;

namespace Client.Common.PrefabPostProcess.MeshMerge
{
    /// <summary>
    /// まとめたメッシュと、サブメッシュ順の元マテリアル
    /// The merged mesh and the source materials in submesh order
    /// </summary>
    public readonly struct MeshMergeResult
    {
        public readonly Mesh Mesh;
        public readonly Material[] Materials;

        public MeshMergeResult(Mesh mesh, Material[] materials)
        {
            Mesh = mesh;
            Materials = materials;
        }
    }
}
