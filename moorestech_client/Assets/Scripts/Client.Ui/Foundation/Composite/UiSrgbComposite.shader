// UI のパネル RT（sRGB の値のまま合成した乗算済みの色と被覆率）を、世界の色を sRGB にしてから over で重ねる（Chrome と同じ sRGB 空間の合成）
// UI が無い画素は捨て、URP が書いた最終の色をそのまま残す
// Composites the UI panel RT (premultiplied colour blended as sRGB values, plus coverage) over the world converted to sRGB (the same sRGB-space over as Chrome)
// Pixels without UI are discarded so URP's final colour stays untouched
Shader "Hidden/Moorestech/UiSrgbComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "UiSrgbComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_UiSrgbTexture);
            float _OutputSrgbEncoded;
            float _WorldPointSample;

            half4 Frag(Varyings input) : SV_Target
            {
                // UI の画素は同じ大きさの RT から点で読む
                // Read the UI pixel point-sampled from the same-sized RT
                float2 uv = input.texcoord.xy;
                float4 ui = SAMPLE_TEXTURE2D_LOD(_UiSrgbTexture, sampler_PointClamp, uv, 0);
                clip(ui.a - 0.5 / 255.0);

                // 世界の色は URP の final blit と同じ拡大フィルタ（点か双線形）で読み、sRGB にして乗算済みの UI を over で重ねる
                // Read the world colour with the same upscaling filter as URP's final blit (point or bilinear), convert to sRGB and put the premultiplied UI over it
                float3 world = _WorldPointSample > 0.5
                    ? SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb
                    : SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
                world = saturate(world);
                float3 composite = ui.rgb + (1.0 - ui.a) * LinearToSRGB(world);
                float3 output = _OutputSrgbEncoded > 0.5 ? composite : SRGBToLinear(composite);
                return half4(output, 1.0);
            }
            ENDHLSL
        }
    }
}
