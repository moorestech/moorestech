using System.Collections.Generic;
using UnityEngine;

namespace Client.Common.PrefabPostProcess.MeshMerge
{
    /// <summary>
    /// まとめたメッシュの置き場所を作り、Rendererを据え付けて元のRendererを消し、LODGroupの一覧も付け替える
    /// Creates the host for a merged mesh, installs its renderer, removes the source renderers, and repoints LODGroup entries
    /// </summary>
    public static class MergedRendererInstaller
    {
        // メッシュはこの置き場所の空間でまとめる(MeshMerger.Mergeのspaceに渡す)
        // The mesh is merged in this host's space (pass it as MeshMerger.Merge's space)
        public static GameObject CreateHost(MergedRendererPlacement placement, int layer)
        {
            var host = new GameObject(placement.Name) { layer = layer };
            host.transform.SetParent(placement.Parent, false);
            host.transform.SetLocalPositionAndRotation(placement.LocalPosition, placement.LocalRotation);
            return host;
        }

        public static MeshRenderer Install(GameObject template, GameObject host, Mesh mesh, Material[] materials, IReadOnlyList<MeshRenderer> sources)
        {
            // 描画の設定は先頭の元Rendererに合わせる
            // Rendering settings follow the first source renderer
            var settings = sources[0];
            host.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = settings.shadowCastingMode;
            renderer.receiveShadows = settings.receiveShadows;
            renderer.lightProbeUsage = settings.lightProbeUsage;
            renderer.reflectionProbeUsage = settings.reflectionProbeUsage;
            renderer.renderingLayerMask = settings.renderingLayerMask;
            renderer.motionVectorGenerationMode = settings.motionVectorGenerationMode;

            ReplaceInLodGroups();
            // Instantiateで複製されないよう即時に消す
            // Destroy immediately so later Instantiate calls do not copy them
            foreach (var source in sources)
            {
                var filter = source.GetComponent<MeshFilter>();
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(filter);
            }
            return renderer;

            #region Internal

            void ReplaceInLodGroups()
            {
                foreach (var lodGroup in template.GetComponentsInChildren<LODGroup>(true))
                {
                    var lods = lodGroup.GetLODs();
                    var changed = false;
                    for (var i = 0; i < lods.Length; i++)
                    {
                        var replaced = new List<Renderer>();
                        var containsSource = false;
                        foreach (var lodRenderer in lods[i].renderers)
                        {
                            if (lodRenderer is MeshRenderer meshRenderer && Contains(meshRenderer)) { containsSource = true; continue; }
                            replaced.Add(lodRenderer);
                        }
                        if (!containsSource) continue;
                        replaced.Add(renderer);
                        lods[i].renderers = replaced.ToArray();
                        changed = true;
                    }
                    if (changed) lodGroup.SetLODs(lods);
                }
            }

            bool Contains(MeshRenderer target)
            {
                foreach (var source in sources)
                {
                    if (source == target) return true;
                }
                return false;
            }

            #endregion
        }
    }
}
