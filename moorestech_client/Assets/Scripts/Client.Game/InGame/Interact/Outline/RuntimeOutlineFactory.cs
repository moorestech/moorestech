using System.Collections.Generic;
using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.Interact.Outline
{
    /// <summary>
    ///     初回ハイライト時に最近傍LODを複製しアウトライン生成
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

            // 生成直後は非表示。呼び出し元で点灯
            // Starts hidden; the caller flips it on with SetActive
            outlineRoot.SetActive(false);
            return outlineRoot;

            #region Internal

            // 最近接LODのレンダラーだけをアウトライン化する。遠景LODまで複製すると輪郭が二重になる
            // Only the nearest LOD is outlined; duplicating the far LODs too would double the silhouette
            List<Renderer> CollectNearestLodRenderers(GameObject collectTarget)
            {
                var renderers = new List<Renderer>();
                var lodGroup = collectTarget.GetComponentInChildren<LODGroup>(true);
                if (lodGroup == null)
                {
                    foreach (var renderer in collectTarget.GetComponentsInChildren<MeshRenderer>(true))
                        if (IsOutlineSource(renderer)) renderers.Add(renderer);
                    return renderers;
                }

                foreach (var renderer in lodGroup.GetLODs()[0].renderers)
                    if (renderer != null && IsOutlineSource(renderer)) renderers.Add(renderer);
                return renderers;
            }

            // 見えているメッシュだけを輪郭の元にする。既存の輪郭と設置プレビュー箱は元にしない
            // Only visible meshes seed the outline; a baked-in outline and the placement preview box never do
            bool IsOutlineSource(Renderer renderer)
            {
                if (!renderer.gameObject.activeInHierarchy) return false;

                var layer = renderer.gameObject.layer;
                return layer != LayerConst.OutlineLayer && layer != LayerConst.BlockBoundingBoxLayer;
            }

            void CreateOutlineMesh(Transform parent, Renderer sourceRenderer, MeshFilter sourceFilter, Material material)
            {
                var outlineMesh = new GameObject(sourceRenderer.name) { layer = LayerConst.OutlineLayer };
                outlineMesh.transform.SetParent(parent, false);
                CopyWorldTransform(sourceRenderer.transform, outlineMesh.transform);

                outlineMesh.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                outlineMesh.AddComponent<MeshRenderer>().sharedMaterials = FillOutlineMaterials(sourceRenderer.sharedMaterials.Length, material);
            }

            void CopyWorldTransform(Transform source, Transform copyTarget)
            {
                copyTarget.SetPositionAndRotation(source.position, source.rotation);

                // 親の拡縮を打ち消して設定
                // Cancel out the parent's accumulated scale before assigning localScale
                var parentScale = copyTarget.parent.lossyScale;
                if (parentScale.x == 0f || parentScale.y == 0f || parentScale.z == 0f)
                {
                    // 親の拡縮が0だとサイズを再現できないので黙って進めず警告する
                    // A zero parent scale cannot be reproduced, so warn instead of silently proceeding
                    Debug.LogWarning($"{copyTarget.name}: parent scale is flattened to zero, outline localScale left unset");
                    return;
                }

                var sourceScale = source.lossyScale;
                copyTarget.localScale = new Vector3(sourceScale.x / parentScale.x, sourceScale.y / parentScale.y, sourceScale.z / parentScale.z);
            }

            Material[] FillOutlineMaterials(int sourceMaterialCount, Material material)
            {
                // サブメッシュごとにスロットが要るので、元と同数（最低1枚）を全部アウトラインで埋める
                // Every submesh needs a slot, so fill as many as the source had, never fewer than one
                var materials = new Material[Mathf.Max(1, sourceMaterialCount)];
                for (var index = 0; index < materials.Length; index++) materials[index] = material;
                return materials;
            }

            #endregion
        }
    }
}
