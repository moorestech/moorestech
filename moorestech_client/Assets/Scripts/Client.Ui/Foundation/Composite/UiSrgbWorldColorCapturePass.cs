using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// 世界色のハンドルを合成パスへ渡す
    /// Hands the world colour handle to the composite pass
    /// </summary>
    /// <remarks>
    /// AfterRendering では URP の作業中の色が画面側へ切り替わって中間テクスチャを指さない。ここに 1 つパスがあると URP はポスト処理の結果を中間テクスチャへ書くので、その時点の作業中の色を控える
    /// At AfterRendering URP's active colour has switched to the screen and no longer points at the intermediate; having a pass here makes URP write the post-processed result into an intermediate texture, so the active colour at this point is recorded
    /// </remarks>
    internal sealed class UiSrgbWorldColorCapturePass : ScriptableRenderPass
    {
        public UiSrgbWorldColorCapturePass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // 画面へ直接書かれた場合は控えず、合成パスがログを出して描かない
            // When post-processing went straight to the screen nothing is recorded, and the composite pass logs and skips
            var resourceData = frameData.Get<UniversalResourceData>();
            var worldColor = resourceData.isActiveTargetBackBuffer ? TextureHandle.nullHandle : resourceData.activeColorTexture;
            frameData.GetOrCreate<UiSrgbWorldColorData>().SetWorldColor(worldColor);
        }
    }
}
