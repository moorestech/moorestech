using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// 世界色と UI RT を sRGB で over 合成
    /// Composites world colour and UI RT with sRGB-space over
    /// </summary>
    /// <remarks>
    /// URP の final blit（AfterRendering + 1）より後に置くのは、描画解像度を下げたときの拡大より後の、画面と同じ解像度で UI を重ねるため。書き先は final blit と同じカメラの最終ターゲット
    /// 中間色は描画解像度のままなので、UI が半透明の画素だけ URP の final blit と同じ拡大フィルタ（Auto で整数倍なら点、それ以外は双線形）で読む。UI が無い画素は URP の結果を残す
    /// 本 repo の URP は互換モード（URP_COMPATIBILITY_MODE）で動くので Execute だけを持つ。Render Graph に切り替えると URP が「RecordRenderGraph が無い」と毎フレーム警告する
    /// It runs after URP's final blit (AfterRendering + 1) so the UI is composited at screen resolution after the upscale of a reduced render scale, writing to the same final camera target as the final blit
    /// The intermediate colour stays at render resolution, so only pixels under translucent UI read it with the same upscaling filter as URP's final blit (point for Auto at an integer scale, bilinear otherwise); pixels without UI keep URP's result
    /// This repo runs URP in compatibility mode (URP_COMPATIBILITY_MODE), so only Execute exists; switching to Render Graph makes URP warn every frame that RecordRenderGraph is missing
    /// </remarks>
    internal sealed class UiSrgbCompositePass : ScriptableRenderPass
    {
        private static readonly int UiSrgbTextureId = Shader.PropertyToID("_UiSrgbTexture");
        private static readonly int OutputSrgbEncodedId = Shader.PropertyToID("_OutputSrgbEncoded");
        private static readonly int WorldPointSampleId = Shader.PropertyToID("_WorldPointSample");
        private static readonly ProfilingSampler Sampler = new("UI sRGB Composite");

        private readonly Material _material;
        private readonly UiSrgbWorldColorCapturePass _worldColorCapture;
        private readonly RTHandle _cameraTarget = RTHandles.Alloc(BuiltinRenderTextureType.CameraTarget);

        public UiSrgbCompositePass(Material material, UiSrgbWorldColorCapturePass worldColorCapture)
        {
            _material = material;
            _worldColorCapture = worldColorCapture;
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRendering + 2);
            // 世界を中間テクスチャへ描かせる
            // Make URP render the world into an intermediate texture
            requiresIntermediateTexture = true;
        }

        public void Dispose()
        {
            RTHandles.Release(_cameraTarget);
        }

#if URP_COMPATIBILITY_MODE
        [System.Obsolete("URP compatibility mode path")]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!UiSrgbCompositeSource.TryGetTexture(out var uiTexture)) return;
            var cameraData = renderingData.cameraData;
            // 同じカメラのポスト処理の後に控えた世界の色を読み、次のカメラへ持ち越さない
            // Read the world colour captured after this camera's post-processing and do not carry it over to the next camera
            var world = _worldColorCapture.WorldColor;
            _worldColorCapture.ClearWorldColor();
            if (world == null || world.rt == null)
            {
                Debug.LogError("[UiSrgbCompositePass] the post-processed world colour was not captured; the UI is not drawn this frame");
                return;
            }

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, Sampler))
            {
                // backbuffer が sRGB 形式でない環境では URP の final blit と同じく自分で sRGB に符号化する
                // Where the backbuffer is not an sRGB format, encode to sRGB ourselves as URP's final blit does
                _material.SetTexture(UiSrgbTextureId, uiTexture);
                _material.SetFloat(OutputSrgbEncodedId, Display.main.requiresSrgbBlitToBackbuffer ? 1f : 0f);
                _material.SetFloat(WorldPointSampleId, WorldUsesPointSampling(cameraData) ? 1f : 0f);
                // 上下の反転は URP の final blit と同じ判定（RenderingUtils.GetFinalBlitScaleBias）。書き先が反転しない画面なら uv を反転する
                // Y flip uses the same test as URP's final blit (RenderingUtils.GetFinalBlitScaleBias): flip the uv when the target is the unflipped screen
                var screen = _cameraTarget;
                var scale = world.useScaling ? world.rtHandleProperties.rtHandleScale : Vector4.one;
                var scaleBias = cameraData.IsRenderTargetProjectionMatrixFlipped(screen) ? new Vector4(scale.x, scale.y, 0, 0) : new Vector4(scale.x, -scale.y, 0, scale.y);

                cmd.SetRenderTarget(screen.nameID, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
                cmd.SetViewport(cameraData.camera.pixelRect);
                Blitter.BlitTexture(cmd, world, scaleBias, _material, 0);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            UiSrgbCompositeSource.MarkComposited();
        }

        private static bool WorldUsesPointSampling(CameraData cameraData)
        {
            // 画面のカメラの描画解像度と、パイプライン asset の拡大フィルタの選択から URP と同じフィルタを選ぶ
            // Pick the same filter as URP from the camera's render scale and the pipeline asset's upscaling filter selection
            var camera = cameraData.camera;
            return UiSrgbWorldUpscaleFilter.UsesPointSampling(cameraData.renderScale, camera.pixelWidth, camera.pixelHeight, UniversalRenderPipeline.asset.upscalingFilter);
        }
#endif
    }
}
