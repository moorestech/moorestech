using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Client.Game.InGame.GraphicsQuality.Appliers
{
    /// <summary>
    /// 描画距離・影・ポストエフェクトをカメラ単位で当てる
    /// Applies draw distance, shadows and post-processing per camera
    /// </summary>
    public static class CameraQualityApplier
    {
        public static void Apply(Camera camera, CinemachineVirtualCamera virtualCamera, GraphicsQualitySettings settings)
        {
            // Cinemachineのlensが毎フレーム上書きするため両方へ入れる
            // Cinemachine's lens overwrites the camera every frame, so set both
            var farClipPlane = settings.DrawDistance.ToMeters();
            camera.farClipPlane = farClipPlane;
            var lens = virtualCamera.m_Lens;
            lens.FarClipPlane = farClipPlane;
            virtualCamera.m_Lens = lens;

            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderShadows = settings.ShadowsEnabled;
            cameraData.renderPostProcessing = settings.PostProcessingEnabled;

            // 海を消すと屈折用の画面コピーも要らない
            // With the ocean hidden, the refraction screen copy is unnecessary
            cameraData.requiresColorOption = settings.OceanVisible ? CameraOverrideOption.UsePipelineSettings : CameraOverrideOption.Off;
        }
    }
}
