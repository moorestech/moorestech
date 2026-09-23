Shader "Moorestech/BeltItemInstanced"
{
    Properties { _BaseMap("Item", 2D) = "white" {} _BaseColor("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"
            StructuredBuffer<float4> _Positions;
            StructuredBuffer<uint> _Offsets;
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _CubeEdge;
            uint _KindIndex;
            CBUFFER_END
            struct Attributes { float3 position : POSITION; float2 uv : TEXCOORD0; uint id : SV_InstanceID; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                InitIndirectDrawArgs(0);
                Varyings output;
                uint index = _Offsets[_KindIndex] + GetIndirectInstanceID(input.id);
                float3 world = _Positions[index].xyz + input.position * _CubeEdge;
                output.position = TransformWorldToHClip(world);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor; }
            ENDHLSL
        }
    }
}
