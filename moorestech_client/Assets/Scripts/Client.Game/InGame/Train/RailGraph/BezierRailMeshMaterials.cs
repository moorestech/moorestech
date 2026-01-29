using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    internal static class BezierRailMeshMaterials
    {
        #region Internal

        // GPU用シェーダーを準備する
        // Prepare deformation shader
        internal static bool EnsureMaterial(BezierRailMesh mesh)
        {
            var shader = ResolveDeformShader(mesh);
            if (shader == null) return false;
            if (mesh._meshRenderer == null && !mesh.TryGetComponent(out mesh._meshRenderer)) return false;

            var baseMaterials = mesh._meshRenderer.sharedMaterials;
            if (baseMaterials == null || baseMaterials.Length == 0) baseMaterials = new[] { mesh._meshRenderer.sharedMaterial };

            var isRuntimeAssigned = ReferenceEquals(mesh._meshRenderer.sharedMaterials, mesh._runtimeMaterials);
            if (mesh._runtimeMaterials == null || mesh._runtimeMaterials.Length != baseMaterials.Length || !isRuntimeAssigned) RebuildRuntimeMaterials(mesh, baseMaterials, shader);
            else ApplyShaderToRuntimeMaterials(mesh, shader);

            if (mesh._propertyBlock == null) mesh._propertyBlock = new MaterialPropertyBlock();
            return mesh._runtimeMaterials != null && mesh._runtimeMaterials.Length > 0;
        }

        // 変形シェーダーを解決する
        // Resolve deformation shader
        internal static Shader ResolveDeformShader(BezierRailMesh mesh)
        {
            if (mesh._deformShader != null) return mesh._deformShader;
            mesh._deformShader = Shader.Find("RailPreview/BezierDeform");
            if (mesh._deformShader == null) Debug.LogError("[BezierRailMesh] Deformation shader not found.");
            return mesh._deformShader;
        }

        // ランタイムマテリアルを再構築する
        // Rebuild runtime materials
        internal static void RebuildRuntimeMaterials(BezierRailMesh mesh, Material[] baseMaterials, Shader shader)
        {
            ReleaseRuntimeMaterials(mesh);
            if (baseMaterials == null || baseMaterials.Length == 0) return;

            mesh._runtimeMaterials = new Material[baseMaterials.Length];
            var previewMaterial = ResolvePreviewMaterial();

            // プレビュー用マテリアルをベースにし、元マテリアルの見た目を上書きする
            // Use preview material base and override with source material data
            for (var i = 0; i < baseMaterials.Length; i++)
            {
                var baseMaterial = baseMaterials[i];
                var runtime = new Material(shader);
                if (previewMaterial != null) runtime.CopyPropertiesFromMaterial(previewMaterial);
                if (baseMaterial != null) ApplyBaseMaterialOverrides(runtime, baseMaterial);
                ApplyPreviewDefaults(mesh, runtime);
                mesh._runtimeMaterials[i] = runtime;
            }

            mesh._meshRenderer.sharedMaterials = mesh._runtimeMaterials;
        }

        // 既存マテリアルのシェーダーを更新する
        // Update shader on existing materials
        internal static void ApplyShaderToRuntimeMaterials(BezierRailMesh mesh, Shader shader)
        {
            if (mesh._runtimeMaterials == null) return;

            // シェーダーのみ差し替える
            // Replace shader only
            for (var i = 0; i < mesh._runtimeMaterials.Length; i++)
            {
                var runtime = mesh._runtimeMaterials[i];
                if (runtime == null) continue;
                if (runtime.shader == shader) continue;
                runtime.shader = shader;
                ApplyPreviewDefaults(mesh, runtime);
            }
        }

        // プレビュー用の初期値を適用する
        // Apply preview defaults
        internal static void ApplyPreviewDefaults(BezierRailMesh mesh, Material runtime)
        {
            if (runtime == null) return;
            if (runtime.HasProperty(BezierRailMesh.ScanlineSpeedId)) runtime.SetFloat(BezierRailMesh.ScanlineSpeedId, 10f);
            if (runtime.HasProperty(BezierRailMesh.PreviewColorId)) runtime.SetColor(BezierRailMesh.PreviewColorId, mesh._previewColor);
        }

        // ランタイムマテリアルを解放する
        // Release runtime materials
        internal static void ReleaseRuntimeMaterials(BezierRailMesh mesh)
        {
            if (mesh._runtimeMaterials == null) return;

            for (var i = 0; i < mesh._runtimeMaterials.Length; i++)
            {
                if (mesh._runtimeMaterials[i] == null) continue;
                Object.Destroy(mesh._runtimeMaterials[i]);
                mesh._runtimeMaterials[i] = null;
            }

            mesh._runtimeMaterials = null;
        }

        // プレビュー用マテリアルを取得する
        // Resolve preview material resource
        internal static Material ResolvePreviewMaterial()
        {
            if (BezierRailMesh._previewMaterialLoaded) return BezierRailMesh._previewBaseMaterial;
            BezierRailMesh._previewBaseMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
            BezierRailMesh._previewMaterialLoaded = true;
            return BezierRailMesh._previewBaseMaterial;
        }

        // 元マテリアルの見た目を反映する
        // Apply source material look
        internal static void ApplyBaseMaterialOverrides(Material runtime, Material source)
        {
            if (runtime == null || source == null) return;
            if (runtime.HasProperty(BezierRailMesh.MainTexId))
            {
                runtime.SetTexture(BezierRailMesh.MainTexId, source.mainTexture);
                runtime.SetTextureOffset(BezierRailMesh.MainTexId, source.mainTextureOffset);
                runtime.SetTextureScale(BezierRailMesh.MainTexId, source.mainTextureScale);
            }
            if (runtime.HasProperty(BezierRailMesh.ColorId)) runtime.SetColor(BezierRailMesh.ColorId, source.color);
        }

        #endregion
    }
}
