using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

namespace Client.Game.InGame.UltraLight
{
    /// <summary>
    ///     開発環境限定の超軽量設定。生成直後のGameObjectから描画負荷の高い要素を剥がす
    ///     Development-only ultra-light preset; strips expensive rendering parts from freshly created GameObjects
    /// </summary>
    public static class UltraLightPreset
    {
        private const float CameraFarClipPlane = 300f;
        private const float TerrainHeightmapPixelError = 10f;

        public static void StripEffects(GameObject root)
        {
            // 参照を握るスクリプトがあるため破棄せず無効化に留める
            // Disable instead of destroying because scripts hold direct references
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var emission = particle.emission;
                emission.enabled = false;
            }
            foreach (var particleRenderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true)) particleRenderer.enabled = false;
            foreach (var visualEffect in root.GetComponentsInChildren<VisualEffect>(true)) visualEffect.enabled = false;
            foreach (var light in root.GetComponentsInChildren<Light>(true)) light.enabled = false;

            // 影は全体で切っているがcaster登録自体も外す
            // Shadows are off globally, but also drop the caster registration itself
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        public static void HideRenderers(GameObject root)
        {
            // Colliderは残し当たり判定と採掘対象は維持する
            // Keep Colliders so hit tests and mining targets still work
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }

        public static void ApplyCamera(Camera camera, CinemachineVirtualCamera virtualCamera)
        {
            // Cinemachineのlensが毎フレーム上書きするため両方へ入れる
            // Cinemachine's lens overwrites the camera every frame, so set both
            camera.farClipPlane = CameraFarClipPlane;
            var lens = virtualCamera.m_Lens;
            lens.FarClipPlane = CameraFarClipPlane;
            virtualCamera.m_Lens = lens;

            // ポストエフェクトと海の屈折用opaqueコピーを止める
            // Stop post-processing and the opaque copy used by ocean refraction
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.antialiasing = AntialiasingMode.None;
        }

        public static void ApplyTerrain(UnityEngine.Terrain terrain)
        {
            terrain.heightmapPixelError = TerrainHeightmapPixelError;
            terrain.drawTreesAndFoliage = false;
            terrain.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
