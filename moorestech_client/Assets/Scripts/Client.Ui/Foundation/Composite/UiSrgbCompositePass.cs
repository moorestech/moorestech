using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
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
    /// URP 17.6 で互換モードが無くなったので Render Graph のパスとして記録する。上下の反転は URP の final blit の Render Graph 版と同じく、読み元と書き先の UV 原点の違いで決める
    /// It runs after URP's final blit (AfterRendering + 1) so the UI is composited at screen resolution after the upscale of a reduced render scale, writing to the same final camera target as the final blit
    /// The intermediate colour stays at render resolution, so only pixels under translucent UI read it with the same upscaling filter as URP's final blit (point for Auto at an integer scale, bilinear otherwise); pixels without UI keep URP's result
    /// URP 17.6 removed compatibility mode, so this records a Render Graph pass; the Y flip follows the Render Graph version of URP's final blit and compares the UV origins of the source and destination
    /// </remarks>
    internal sealed class UiSrgbCompositePass : ScriptableRenderPass
    {
        private static readonly int UiSrgbTextureId = Shader.PropertyToID("_UiSrgbTexture");
        private static readonly int OutputSrgbEncodedId = Shader.PropertyToID("_OutputSrgbEncoded");
        private static readonly int WorldPointSampleId = Shader.PropertyToID("_WorldPointSample");
        private static readonly ProfilingSampler Sampler = new("UI sRGB Composite");

        private readonly Material _material;

        public UiSrgbCompositePass(Material material)
        {
            _material = material;
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRendering + 2);
            // 世界を中間テクスチャへ描かせる
            // Make URP render the world into an intermediate texture
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!UiSrgbCompositeSource.TryGetTexture(out var uiTexture)) return;

            // 同じカメラのポスト処理の後に控えた世界の色を読み、次のカメラへ持ち越さない
            // Read the world colour captured after this camera's post-processing and do not carry it over to the next camera
            var world = TextureHandle.nullHandle;
            if (frameData.Contains<UiSrgbWorldColorData>())
            {
                var worldColorData = frameData.Get<UiSrgbWorldColorData>();
                world = worldColorData.WorldColor;
                worldColorData.Reset();
            }
            if (!world.IsValid())
            {
                Debug.LogError("[UiSrgbCompositePass] the post-processed world colour was not captured; the UI is not drawn this frame");
                return;
            }

            // 書き先は final blit 後の作業中の色（画面）。シェーダが UI の無い画素を捨てるので中身を読み込んで残す
            // Write to the active colour after the final blit (the screen); the shader discards pixels without UI, so the contents are loaded and kept
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            using var builder = renderGraph.AddRasterRenderPass<CompositePassData>("UI sRGB Composite", out var passData, Sampler);
            passData.World = world;
            passData.Screen = resourceData.activeColorTexture;
            passData.Material = _material;
            passData.UiTexture = uiTexture;
            passData.PixelRect = cameraData.camera.pixelRect;
            passData.OutputSrgbEncoded = Display.main.requiresSrgbBlitToBackbuffer;
            passData.WorldPointSample = UsesWorldPointSampling();
            builder.UseTexture(world, AccessFlags.Read);
            builder.SetRenderAttachment(passData.Screen, 0, AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) => ExecuteComposite(data, context));

            #region Internal

            bool UsesWorldPointSampling()
            {
                // 画面のカメラの描画解像度と、パイプライン asset の拡大フィルタの選択から URP と同じフィルタを選ぶ
                // Pick the same filter as URP from the camera's render scale and the pipeline asset's upscaling filter selection
                var camera = cameraData.camera;
                return UiSrgbWorldUpscaleFilter.UsesPointSampling(cameraData.postProcessEnabled, UsesFinalPointUpscale(), cameraData.renderScale, camera.pixelWidth, camera.pixelHeight, UniversalRenderPipeline.asset.upscalingFilter);
            }

            bool UsesFinalPointUpscale()
            {
                // URP の最終ポスト処理パスで点の拡大が選ばれうるのは FXAA のときだけ。TAA の CAS は RCAS で鋭くするので点にならず、FSR は拡大フィルタ側で扱う
                // Only FXAA lets URP's final post-processing pass pick a point upscale; TAA's CAS sharpens with RCAS instead, and FSR is handled by the filter selection
                return cameraData.antialiasing == AntialiasingMode.FastApproximateAntialiasing;
            }

            #endregion
        }

        private static void ExecuteComposite(CompositePassData data, RasterGraphContext context)
        {
            // backbuffer が sRGB 形式でない環境では URP の final blit と同じく自分で sRGB に符号化する
            // Where the backbuffer is not an sRGB format, encode to sRGB ourselves as URP's final blit does
            data.Material.SetTexture(UiSrgbTextureId, data.UiTexture);
            data.Material.SetFloat(OutputSrgbEncodedId, data.OutputSrgbEncoded ? 1f : 0f);
            data.Material.SetFloat(WorldPointSampleId, data.WorldPointSample ? 1f : 0f);

            // 拡大率と上下の反転は URP の final blit（RenderingUtils.GetFinalBlitScaleBias の Render Graph 版）と同じ式
            // Scale and Y flip use the same formula as URP's final blit (the Render Graph version of RenderingUtils.GetFinalBlitScaleBias)
            RTHandle world = data.World;
            var scale = world.useScaling ? world.rtHandleProperties.rtHandleScale : Vector4.one;
            var yFlip = context.GetTextureUVOrigin(data.World) != context.GetTextureUVOrigin(data.Screen);
            var scaleBias = yFlip ? new Vector4(scale.x, -scale.y, 0, scale.y) : new Vector4(scale.x, scale.y, 0, 0);

            context.cmd.SetViewport(data.PixelRect);
            Blitter.BlitTexture(context.cmd, world, scaleBias, data.Material, 0);
            UiSrgbCompositeSource.MarkComposited();
        }

        private sealed class CompositePassData
        {
            public TextureHandle World;
            public TextureHandle Screen;
            public Material Material;
            public RenderTexture UiTexture;
            public Rect PixelRect;
            public bool OutputSrgbEncoded;
            public bool WorldPointSample;
        }
    }
}
