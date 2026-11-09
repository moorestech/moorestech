using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// ポスト処理を終えた世界の色（中間テクスチャ）のハンドルを、最後に UI を重ねるパスへ渡す
    /// Hands the handle of the post-processed world colour (intermediate texture) to the pass that composites the UI at the end
    /// </summary>
    /// <remarks>
    /// AfterRendering では URP のハンドルが画面側へ切り替わって中間テクスチャを指さない。ここに 1 つパスがあると URP はポスト処理の結果を中間テクスチャへ書く
    /// At AfterRendering URP's handle has switched to the screen and no longer points at the intermediate; having a pass here makes URP write the post-processed result into an intermediate texture
    /// </remarks>
    internal sealed class UiSrgbWorldColorCapturePass : ScriptableRenderPass
    {
        public RTHandle WorldColor { get; private set; }

        public UiSrgbWorldColorCapturePass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public void ClearWorldColor()
        {
            WorldColor = null;
        }

#if URP_COMPATIBILITY_MODE
        [System.Obsolete("URP compatibility mode path")]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            WorldColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
        }
#endif
    }
}
