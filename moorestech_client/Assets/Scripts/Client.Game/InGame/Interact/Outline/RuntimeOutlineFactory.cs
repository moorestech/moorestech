using System.Collections.Generic;
using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.Interact.Outline
{
    /// <summary>
    ///     ブロック・列車・露頭の初回ハイライト時に最近傍LODのメッシュを複製し、実行時アウトラインを生成する
    ///     Builds a runtime outline by duplicating the nearest-LOD meshes the first time a block, train car or outcrop is highlighted
    /// </summary>
    public static class RuntimeOutlineFactory
    {
        private const string OutlineObjectName = "Outline";

        public static GameObject Create(GameObject target)
        {
            var outlineMaterial = MaterialConst.GetInteractOutlineMaterial();

            var outlineRoot = new GameObject(OutlineObjectName) { layer = LayerConst.OutlineLayer };
            outlineRoot.transform.SetParent(target.transform, false);

            foreach (var sourceRenderer in CollectNearestLodRenderers(target))
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

        // 最近接LODのレンダラーだけをアウトライン化する。遠景LODまで複製すると輪郭が二重になる
        // Only the nearest LOD is outlined; duplicating the far LODs too would double the silhouette
        private static List<Renderer> CollectNearestLodRenderers(GameObject target)
        {
            var renderers = new List<Renderer>();
            var lodGroup = target.GetComponentInChildren<LODGroup>(true);
            if (lodGroup == null)
            {
                // 焼き込みアウトラインを既に持つ子（Outlineレイヤ）は複製対象から除く
                // Excludes children already on the Outline layer (a baked-in outline) from duplication
                foreach (var renderer in target.GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.gameObject.layer != LayerConst.OutlineLayer) renderers.Add(renderer);
                return renderers;
            }

            foreach (var renderer in lodGroup.GetLODs()[0].renderers)
                if (renderer != null) renderers.Add(renderer);
            return renderers;
        }

        private static void CreateOutlineMesh(Transform parent, Renderer sourceRenderer, MeshFilter sourceFilter, Material outlineMaterial)
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
            if (parentScale.x == 0f || parentScale.y == 0f || parentScale.z == 0f)
            {
                // 親の拡縮が0だとサイズを再現できないので黙って進めず警告する
                // A zero parent scale cannot be reproduced, so warn instead of silently proceeding
                Debug.LogWarning($"{target.name}: parent scale is flattened to zero, outline localScale left unset");
                return;
            }

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
