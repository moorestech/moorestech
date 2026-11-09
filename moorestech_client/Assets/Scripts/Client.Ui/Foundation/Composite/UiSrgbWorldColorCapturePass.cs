using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// 世界色のハンドルを合成パスへ渡す
    /// Hands the world colour handle to the composite pass
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
