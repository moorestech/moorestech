using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// UI の RT を sRGB で世界へ重ねる差し込み口
    /// URP hook compositing the UI RT over the world in sRGB
    /// </summary>
    /// <remarks>
    /// URP Data・URP Data - Skit の renderer に 1 つずつ載る。パネル RT が無い間（GameUi を使っていない間）は何も足さない
    /// One instance sits on each of the URP Data and URP Data - Skit renderers; nothing is added while there is no panel RT (GameUi not in use)
    /// </remarks>
    public class UiSrgbCompositeFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader compositeShader;

        private Material _material;
        private UiSrgbWorldColorCapturePass _capturePass;
        private UiSrgbCompositePass _pass;

        public override void Create()
        {
            // シェーダが外れていたら起動の段階で止める（UI が画面に出なくなるため）
            // Stop at creation when the shader is missing (the UI would never reach the screen)
            if (compositeShader == null) throw new System.InvalidOperationException("[UiSrgbCompositeFeature] compositeShader is not assigned on the URP renderer data");
            _material = CoreUtils.CreateEngineMaterial(compositeShader);
            _capturePass = new UiSrgbWorldColorCapturePass();
            _pass = new UiSrgbCompositePass(_material, _capturePass);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // 画面へ出す Game カメラだけに足す（シーンビュー・RT へ描くカメラ・プレビューは除く）
            // Add only for Game cameras drawing to the screen (not the scene view, RT cameras or previews)
            var cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game) return;
            if (cameraData.camera.targetTexture != null) return;
            if (!cameraData.resolveFinalTarget) return;
            if (!UiSrgbCompositeSource.TryGetTexture(out _)) return;
            if (!UiSrgbCompositeSource.IsCompositingCamera(cameraData.camera)) return;
            renderer.EnqueuePass(_capturePass);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            CoreUtils.Destroy(_material);
        }
    }
}
