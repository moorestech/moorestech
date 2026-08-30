using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.Interact.Outline
{
    /// <summary>
    ///     ブロック・列車・露頭の初回ハイライト時に複製メッシュのアウトラインを実行時生成する
    ///     Builds a duplicate-mesh outline at runtime the first time a block, train car or outcrop is highlighted
    /// </summary>
    public static class RuntimeOutlineFactory
    {
        private const string OutlineRootName = "InteractOutline";

        public static GameObject Create(GameObject target)
        {
            var outlineMaterial = MaterialConst.GetInteractOutlineMaterial();

            var outlineRoot = new GameObject(OutlineRootName) { layer = LayerConst.OutlineLayer };
            outlineRoot.transform.SetParent(target.transform, false);

            foreach (var sourceRenderer in target.GetComponentsInChildren<MeshRenderer>(true))
            {
                var sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null) continue;

                CreateOutlineMesh(outlineRoot.transform, sourceRenderer, sourceFilter, outlineMaterial);
            }

            // 生成直後は非表示。呼び出し元がSetActiveで点ける
            // Starts hidden; the caller flips it on with SetActive
            outlineRoot.SetActive(false);
            return outlineRoot;
        }

        private static void CreateOutlineMesh(Transform parent, MeshRenderer sourceRenderer, MeshFilter sourceFilter, Material outlineMaterial)
        {
            var outlineMesh = new GameObject(sourceRenderer.name) { layer = LayerConst.OutlineLayer };
            outlineMesh.transform.SetParent(parent, false);
            CopyWorldTransform(sourceRenderer.transform, outlineMesh.transform);

            outlineMesh.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            outlineMesh.AddComponent<MeshRenderer>().sharedMaterials = FillOutlineMaterials(sourceRenderer.sharedMaterials.Length, outlineMaterial);
        }

        private static void CopyWorldTransform(Transform source, Transform target)
        {
            target.SetPositionAndRotation(source.position, source.rotation);

            // localScaleは親の合成拡縮を打ち消してから入れる
            // Cancel out the parent's accumulated scale before assigning localScale
            var parentScale = target.parent.lossyScale;
            if (parentScale.x == 0f || parentScale.y == 0f || parentScale.z == 0f) return;

            var sourceScale = source.lossyScale;
            target.localScale = new Vector3(sourceScale.x / parentScale.x, sourceScale.y / parentScale.y, sourceScale.z / parentScale.z);
        }

        private static Material[] FillOutlineMaterials(int sourceMaterialCount, Material outlineMaterial)
        {
            // サブメッシュごとにスロットが要るので、元と同数（最低1枚）を全部アウトラインで埋める
            // Every submesh needs a slot, so fill as many as the source had, never fewer than one
            var materials = new Material[Mathf.Max(1, sourceMaterialCount)];
            for (var index = 0; index < materials.Length; index++) materials[index] = outlineMaterial;
            return materials;
        }
    }
}
