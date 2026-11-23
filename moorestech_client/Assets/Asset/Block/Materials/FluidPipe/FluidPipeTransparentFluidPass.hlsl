#ifndef MOORESTECH_FLUID_PIPE_TRANSPARENT_FLUID_PASS_INCLUDED
#define MOORESTECH_FLUID_PIPE_TRANSPARENT_FLUID_PASS_INCLUDED

// FluidPipeTransparent の ForwardLit のフラグメント。窓は URP/Lit の LitPassFragment へ渡し、水と水位はここで旧 Shader Graph と同じ計算をする
// FluidPipeTransparent's ForwardLit fragment; the window goes to URP/Lit's LitPassFragment and the water and level are computed here like the old Shader Graph

// 旧 Shader Graph の水の色（sRGB の定数）。Shader Graph と同じ式で線形へ直す
// The old Shader Graph's water color (an sRGB constant), converted to linear with the same expression as Shader Graph
#define FLUID_PIPE_WATER_COLOR float3(0.3831879, 0.6298496, 0.745283)

// 水と水位: 旧 Shader Graph（PBRForwardPass と FluidPipe_Water のグラフ）と同じ計算を、URP/Lit の頂点出力から行う
// Water and level: the same computation as the old Shader Graph (PBRForwardPass with the FluidPipe_Water graph), from URP/Lit's vertex output
void FluidFragment(
    Varyings input
    , out half4 outColor
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers
#endif
)
{
    // 物体座標の z が (1 - 水位) - 0.5 以上の所だけ描く。水位の Quad は _WaterLevel=10 で常に描く
    // Draw only where the object-space z is at least (1 - level) - 0.5; the level quad always draws with _WaterLevel=10
    float edge = (1 - _WaterLevel) + float(-0.5);
    float positionOSZ = TransformWorldToObject(input.positionWS)[2];
    half alpha = step(edge, positionOSZ) * _Alpha;

    // 旧グラフの法線は未設定（白）のテクスチャを法線として読んでいた
    // The old graph read an unassigned (white) texture as the normal
    float3 graphNormalTS = float3(1, 1, 1);

    SurfaceData surfaceData;
#ifdef UNITY_COLORSPACE_GAMMA
    surfaceData.albedo = FLUID_PIPE_WATER_COLOR;
#else
    surfaceData.albedo = SRGBToLinear(FLUID_PIPE_WATER_COLOR);
#endif
    surfaceData.metallic = saturate(float(0));
    surfaceData.specular = 0;
    surfaceData.smoothness = saturate(float(0));
    surfaceData.occlusion = float(0.1);
    surfaceData.emission = float3(0, 0, 0);
    surfaceData.alpha = saturate(alpha);
    surfaceData.normalTS = graphNormalTS;
    surfaceData.clearCoatMask = 0;
    surfaceData.clearCoatSmoothness = 1;
    surfaceData.albedo = AlphaModulate(surfaceData.albedo, surfaceData.alpha);

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    // URP/Lit の tangentWS.w は符号に負のスケールを掛け済みなので、旧グラフが見ていた元の符号へ戻してから同じ式で求める
    // URP/Lit's tangentWS.w already includes the negative scale, so recover the raw sign the old graph saw and apply its expression
    float rawTangentSign = input.tangentWS.w * GetOddNegativeScale();
    float crossSign = (rawTangentSign > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
    float3 bitangent = crossSign * cross(input.normalWS.xyz, input.tangentWS.xyz);
    inputData.tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
    inputData.normalWS = TransformTangentToWorld(graphNormalTS, inputData.tangentToWorld);
    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
#else
    inputData.vertexSH = input.vertexSH;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
#endif
#endif
    SETUP_DEBUG_TEXTURE_DATA(inputData, input.uv);

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, true);

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

void FluidPipeFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    // 材質ごとの値で分けるだけなので variant は1つのまま
    // Branches only on a per-material value, so the variant stays single
    UNITY_BRANCH
    if (_FluidMode > 0.5)
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        FluidFragment(input, outColor
    #ifdef _WRITE_RENDERING_LAYERS
            , outRenderingLayers
    #endif
        );
    }
    else
    {
        LitPassFragment(input, outColor
    #ifdef _WRITE_RENDERING_LAYERS
            , outRenderingLayers
    #endif
        );
    }
}

#endif
