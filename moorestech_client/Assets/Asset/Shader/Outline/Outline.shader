Shader "Unlit/Outline"
{
    Properties
    {
        _OutlineWidth ("Outline Width", float) = 0
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
        ENDHLSL

        Pass
        {
            // アウトラインを拡張したステンシル
            Tags { "LightMode" = "OutlineStencil" }

            ColorMask 0
            ZWrite Off

            Stencil
            {
                Ref 2
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 positionOS: POSITION;
                float4 normalOS: NORMAL;
                float4 tangentOS: TANGENT;
            };

            struct v2f
            {
                float4 positionCS: SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                VertexNormalInputs vertexNormalInputs = GetVertexNormalInputs(v.normalOS.xyz, v.tangentOS);
                
                float3 normalWS = vertexNormalInputs.normalWS;
                float3 normalCS = TransformWorldToHClipDir(normalWS);
                
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = positionInputs.positionCS + float4(normalCS.xy * 0.001 * _OutlineWidth, 0, 0);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 col = _OutlineColor;
                return col;
            }

            ENDHLSL

        }

        Pass
        {
            // 通常描画領域にステンシルを上書き
            Tags { "LightMode" = "FillOutlineStencil" }
            
            ColorMask 0
            ZWrite Off
            ZTest Always

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 positionOS: POSITION;
                float4 normalOS: NORMAL;
                float4 tangentOS: TANGENT;
            };

            struct v2f
            {
                float4 positionCS: SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = positionInputs.positionCS;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                return 0;
            }

            ENDHLSL

        }

        Pass
        {
            // アウトライン描画パス
            Tags { "LightMode" = "Outline" }
            
            ZWrite On

            Stencil
            {
                Ref 2
                Comp Equal
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 positionOS: POSITION;
                float4 normalOS: NORMAL;
                float4 tangentOS: TANGENT;
            };

            struct v2f
            {
                float4 positionCS: SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                VertexNormalInputs vertexNormalInputs = GetVertexNormalInputs(v.normalOS.xyz, v.tangentOS);
                
                float3 normalWS = vertexNormalInputs.normalWS;
                float3 normalCS = TransformWorldToHClipDir(normalWS);
                
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = positionInputs.positionCS + float4(normalCS.xy * 0.001 * _OutlineWidth, 0, 0);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                return _OutlineColor;
            }

            ENDHLSL

        }
    }
}
