#ifndef MOORESTECH_GEAR_ROTATION_VERTEX_INCLUDED
#define MOORESTECH_GEAR_ROTATION_VERTEX_INCLUDED

// URPのLitの各パスの頂点関数の前に、頂点を回す処理を挟む。パスのincludeの後で、パスを表すdefineを付けて読む
// Wraps each URP Lit pass vertex function with the gear spin; include after the pass include with the define naming the pass

#include "GearRotation.hlsl"

#define GEAR_ROTATION_INPUTS float4 gearAxisAndSpeed : TEXCOORD6, float4 gearOriginAndMode : TEXCOORD7

#if defined(GEAR_ROTATION_PASS_FORWARD)
Varyings GearRotationLitPassVertex(Attributes input, GEAR_ROTATION_INPUTS)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3x3 rotation = GearRotationMatrix(gearAxisAndSpeed, gearOriginAndMode);
    input.positionOS.xyz = mul(rotation, input.positionOS.xyz);
    input.normalOS = mul(rotation, input.normalOS);
    input.tangentOS.xyz = mul(rotation, input.tangentOS.xyz);
    return LitPassVertex(input);
}
#endif

#if defined(GEAR_ROTATION_PASS_GBUFFER)
Varyings GearRotationGBufferPassVertex(Attributes input, GEAR_ROTATION_INPUTS)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3x3 rotation = GearRotationMatrix(gearAxisAndSpeed, gearOriginAndMode);
    input.positionOS.xyz = mul(rotation, input.positionOS.xyz);
    input.normalOS = mul(rotation, input.normalOS);
    input.tangentOS.xyz = mul(rotation, input.tangentOS.xyz);
    return LitGBufferPassVertex(input);
}
#endif

#if defined(GEAR_ROTATION_PASS_SHADOW)
Varyings GearRotationShadowPassVertex(Attributes input, GEAR_ROTATION_INPUTS)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3x3 rotation = GearRotationMatrix(gearAxisAndSpeed, gearOriginAndMode);
    input.positionOS.xyz = mul(rotation, input.positionOS.xyz);
    input.normalOS = mul(rotation, input.normalOS);
    return ShadowPassVertex(input);
}
#endif

#if defined(GEAR_ROTATION_PASS_DEPTH_ONLY)
Varyings GearRotationDepthOnlyVertex(Attributes input, GEAR_ROTATION_INPUTS)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3x3 rotation = GearRotationMatrix(gearAxisAndSpeed, gearOriginAndMode);
    input.position.xyz = mul(rotation, input.position.xyz);
    #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    input.normalOS = mul(rotation, input.normalOS);
    input.tangentOS.xyz = mul(rotation, input.tangentOS.xyz);
    #endif
    return DepthOnlyVertex(input);
}
#endif

#if defined(GEAR_ROTATION_PASS_DEPTH_NORMALS)
Varyings GearRotationDepthNormalsVertex(Attributes input, GEAR_ROTATION_INPUTS)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3x3 rotation = GearRotationMatrix(gearAxisAndSpeed, gearOriginAndMode);
    input.positionOS.xyz = mul(rotation, input.positionOS.xyz);
    input.normal = mul(rotation, input.normal);
    input.tangentOS.xyz = mul(rotation, input.tangentOS.xyz);
    return DepthNormalsVertex(input);
}
#endif

#if defined(GEAR_ROTATION_PASS_MOTION_VECTORS)
// 前フレームの位置も今の角度で回し、回転による動きは動きベクトルに含めない
// The previous position is spun by the current angle too, so the spin is left out of motion vectors
Varyings GearRotationMotionVectorVertex(Attributes input, GEAR_ROTATION_INPUTS)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3x3 rotation = GearRotationMatrix(gearAxisAndSpeed, gearOriginAndMode);
    input.position.xyz = mul(rotation, input.position.xyz);
    input.positionOld = mul(rotation, input.positionOld);
    return vert(input);
}
#endif

#endif
