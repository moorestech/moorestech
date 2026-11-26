using Client.Common.PrefabPostProcess;
using Client.Common.PrefabPostProcess.MeshMerge;
using Client.Game.InGame.Block.GearRotation.Gpu;
using Client.Game.InGame.BlockSystem.StateProcessor;
using UnityEngine;

namespace Client.Game.InGame.Block.GearRotation.PrefabPostProcess
{
    /// <summary>
    /// 歯車の回す部品ごとに、下のRendererを1つのメッシュへまとめ、シェーダーで回るマテリアルへ替える
    /// まとめられない部品は今までどおりCPUで回す(理由をログへ出す)
    /// For each rotating gear part, merges the renderers underneath into one mesh and swaps to materials the shader spins
    /// Parts that cannot be merged keep spinning on the CPU (the reason is logged)
    /// </summary>
    public sealed class GearRotationPrefabPostProcessor : IPrefabPostProcessor
    {
        private readonly GearRotationRenderSettings _settings;

        public GearRotationPrefabPostProcessor(GearRotationRenderSettings settings)
        {
            _settings = settings;
            // アイコン撮影などメインゲームの前に描かれても、空の表が結び付いているようにする
            // Bind an empty table so drawing before the main game (icon capture etc.) still has one
            GearRotationGpuTable.Upload(0f);
        }

        public bool IsTarget(GameObject prefab)
        {
            return prefab.GetComponentInChildren<GearStateChangeProcessor>(true) != null;
        }

        public void Process(GameObject template, PrefabPostProcessContext context)
        {
            foreach (var processor in template.GetComponentsInChildren<GearStateChangeProcessor>(true))
            {
                foreach (var rotationInfo in processor.RotationInfos)
                {
                    if (rotationInfo is not TransformRotationInfo info) continue;
                    if (!GearRotationMergeEligibility.TryCollect(template, info, context, _settings.SourceLitShader, out var renderers, out var reason))
                    {
                        Debug.Log($"[GearRotationPrefabPostProcessor] {template.name}/{(info.RotationTransform == null ? "null" : info.RotationTransform.name)} はCPUで回す: {reason}");
                        continue;
                    }
                    MoveToShaderRotation(info, renderers);
                }
            }

            #region Internal

            void MoveToShaderRotation(TransformRotationInfo info, System.Collections.Generic.List<MeshRenderer> renderers)
            {
                // 回すTransformと同じ姿勢・大きさ1の置き場所を作り、その空間でまとめる。大きさは頂点へ焼き込む
                // Create a host with the rotating transform's pose at unit scale and merge in its space; scale is baked into vertices
                var rotationTransform = info.RotationTransform;
                var placement = new MergedRendererPlacement(rotationTransform.name + " (GearRotation)", rotationTransform.parent, rotationTransform.localPosition, rotationTransform.localRotation);
                var host = MergedRendererInstaller.CreateHost(placement, rotationTransform.gameObject.layer);
                var blockOrigin = host.transform.InverseTransformPoint(template.transform.position);
                var merged = MeshMerger.Merge(host.transform, renderers, new GearRotationVertexWriter(info, blockOrigin));
                SetSpinBounds(merged.Mesh);

                var materials = new Material[merged.Materials.Length];
                for (var i = 0; i < materials.Length; i++) materials[i] = context.MaterialVariants.GetOrCreate(merged.Materials[i], _settings.RotatingLitShader);
                var renderer = MergedRendererInstaller.Install(template, host, merged.Mesh, materials, renderers);
                context.Claim(renderer);
                info.SetGpuDriven();
            }

            #endregion
        }

        // 回転軸まわりのどの角度でも収まるよう、原点中心の球を囲む箱にする
        // Use the box around an origin-centered sphere so every spin angle stays inside the bounds
        private static void SetSpinBounds(Mesh mesh)
        {
            var radius = 0f;
            foreach (var vertex in mesh.vertices) radius = Mathf.Max(radius, vertex.magnitude);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (radius * 2f));
        }
    }
}
