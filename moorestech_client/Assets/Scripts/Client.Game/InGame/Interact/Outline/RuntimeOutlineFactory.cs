using System.Collections.Generic;
using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.Interact.Outline
{
    /// <summary>
    ///     最近傍LODを複製してアウトラインを作る唯一の場所。実行時のハイライトとEditorの焼き込みが同じ手順を通る
    ///     The single place outlines are built by duplicating the nearest LOD; runtime highlighting and the Editor bake share this one procedure
    /// </summary>
    public static class RuntimeOutlineFactory
    {
        private const string OutlineObjectName = "Outline";

        // 生成失敗（複製できるメッシュが無い）を空の墓標として残すと二度と作り直されないので、都度作り直す形にする
        // Caching a failed build as an empty tombstone would freeze it forever, so a failure simply retries on the next highlight
        public static void Apply(GameObject target, ref GameObject outlineObject, bool highlighted)
        {
            if (highlighted && outlineObject == null) outlineObject = Create(target);
            if (outlineObject != null) outlineObject.SetActive(highlighted);
        }

        public static GameObject Create(GameObject target)
        {
            return Create(target, MaterialConst.GetInteractOutlineMaterial());
        }

        // 輪郭を1枚も作れなければnullを返す。呼び出し側が「作れた」と誤認しないための唯一の合図
        // Returns null when not one outline mesh could be built; that is the only signal callers get that the build failed
        public static GameObject Create(GameObject target, Material outlineMaterial)
        {
            var outlineRoot = new GameObject(OutlineObjectName) { layer = LayerConst.OutlineLayer };
            outlineRoot.transform.SetParent(target.transform, false);

            foreach (var sourceRenderer in CollectNearestLodRenderers(target))
            {
                var sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null) continue;

                CreateOutlineMesh(outlineRoot.transform, sourceRenderer, sourceFilter, outlineMaterial);
            }

            if (outlineRoot.transform.childCount == 0)
            {
                Object.DestroyImmediate(outlineRoot);
                return null;
            }

            // 生成直後は非表示。呼び出し元で点灯
            // Starts hidden; the caller flips it on with SetActive
            outlineRoot.SetActive(false);
            return outlineRoot;

            #region Internal

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
                if (Mathf.Approximately(parentScale.x, 0f) || Mathf.Approximately(parentScale.y, 0f) || Mathf.Approximately(parentScale.z, 0f))
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

        // 最近接LODのレンダラーだけをアウトライン化する。遠景LODまで複製すると輪郭が二重になる
        // Only the nearest LOD is outlined; duplicating the far LODs too would double the silhouette
        public static List<Renderer> CollectNearestLodRenderers(GameObject target)
        {
            var renderers = new List<Renderer>();
            var lodGroup = target.GetComponentInChildren<LODGroup>(true);
            if (lodGroup == null)
            {
                foreach (var renderer in target.GetComponentsInChildren<MeshRenderer>(true))
                    if (IsOutlineSource(renderer)) renderers.Add(renderer);
                return renderers;
            }

            foreach (var renderer in lodGroup.GetLODs()[0].renderers)
                if (renderer != null && IsOutlineSource(renderer)) renderers.Add(renderer);
            return renderers;

            #region Internal

            // 見えているメッシュだけを輪郭の元にする。既存の輪郭と設置プレビュー箱は元にしない
            // Only visible meshes seed the outline; a baked-in outline and the placement preview box never do
            bool IsOutlineSource(Renderer renderer)
            {
                if (!renderer.gameObject.activeInHierarchy) return false;

                var layer = renderer.gameObject.layer;
                return layer != LayerConst.OutlineLayer && layer != LayerConst.BlockBoundingBoxLayer;
            }

            #endregion
        }
    }
}
