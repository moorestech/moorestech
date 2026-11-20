#ifndef MOORESTECH_FLUID_PIPE_TRANSPARENT_INPUT_INCLUDED
#define MOORESTECH_FLUID_PIPE_TRANSPARENT_INPUT_INCLUDED

// パイプの窓・水・水位を同じ variant で描くための入力。窓は URP/Lit（LitInput.hlsl）と同じ計算をし、水と水位は旧 Shader Graph と同じ定数を使う
// Inputs for drawing the pipe's window, water and water level in one variant; the window computes exactly like URP/Lit (LitInput.hlsl), the water and level use the old Shader Graph constants

// 窓の材質が使っていたキーワードを固定する。材質ごとにキーワードが違うと variant が分かれ、SetPass が戻ってしまう
// Fix the keywords the window material used; per-material keywords would split the variant and bring the SetPass calls back
// 下の URP の hlsl（SurfaceInput の SampleNormal など）がこれを見るので、インクルードより前に置く
// URP's hlsl below (such as SurfaceInput's SampleNormal) reads these, so they come before the includes
#define _NORMALMAP 1
#define _OCCLUSIONMAP 1
#define _PARALLAXMAP 1
#define _SURFACE_TYPE_TRANSPARENT 1
#define _ALPHAPREMULTIPLY_ON 1

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
half4 _BaseColor;
half4 _EmissionColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _Parallax;
half _OcclusionStrength;
half _Surface;
// 0: 窓（URP/Lit と同じ計算）、1: 水と水位（旧 Shader Graph と同じ計算）
// 0: window (same as URP/Lit), 1: water and water level (same as the old Shader Graph)
float _FluidMode;
float _WaterLevel;
float _Alpha;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

TEXTURE2D(_ParallaxMap);        SAMPLER(sampler_ParallaxMap);
TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);

void ApplyPerPixelDisplacement(half3 viewDirTS, inout float2 uv)
{
    uv += ParallaxMapping(TEXTURE2D_ARGS(_ParallaxMap, sampler_ParallaxMap), viewDirTS, _Parallax, uv);
}

// LitInput.hlsl の InitializeStandardLitSurfaceData を、窓のキーワード（法線・Occlusion・視差、金属度はテクスチャなし）で展開したもの。LitForwardPass.hlsl から呼ばれる
// LitInput.hlsl's InitializeStandardLitSurfaceData expanded for the window's keywords (normal, occlusion, parallax; metallic without a texture); called from LitForwardPass.hlsl
inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);

    half4 specGloss;
    specGloss.rgb = _Metallic.rrr;
    specGloss.a = _Smoothness;
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.albedo = AlphaModulate(outSurfaceData.albedo, outSurfaceData.alpha);

    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0, 0.0, 0.0);

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
    outSurfaceData.occlusion = LerpWhiteTo(occ, _OcclusionStrength);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));

    outSurfaceData.clearCoatMask       = half(0.0);
    outSurfaceData.clearCoatSmoothness = half(0.0);
}

#endif
