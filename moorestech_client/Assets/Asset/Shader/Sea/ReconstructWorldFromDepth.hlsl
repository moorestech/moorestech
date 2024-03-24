// 参考 https://zenn.dev/r_ngtm/articles/shadergraph-reconstruct-wpos-depth


// Precision : float
void ReconstructWorldFromDepth_float(float2 ScreenPosition, float Depth, float4x4 unity_MatrixInvVP, out float3 Out)
{
    // スクリーン座標とDepthからクリップ座標を作成
    float4 positionCS = float4(ScreenPosition * 2.0 - 1.0, Depth, 1.0);

    #if UNITY_UV_STARTS_AT_TOP
    positionCS.y = -positionCS.y;
    #endif

    // クリップ座標にView Projection変換を適用し、ワールド座標にする    
    float4 hpositionWS = mul(unity_MatrixInvVP, positionCS);

    // 同次座標系を標準座標系に戻す
    Out = hpositionWS.xyz / hpositionWS.w;
}