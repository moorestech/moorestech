Shader "Hobione/Skybox/ObjectSkybox6side"
{
    // Copyright (c) 2021 ほびわん (zlib license)
    Properties{
        [Header(Color)]
        _Tint("Tint Color", Color) = (0.5, 0.5, 0.5, 0.5)
        [Gamma] _Exposure("Exposure", Range(0, 8)) = 1
        _Opacity("Opacity", Range(0,1)) = 1.0

        [Header(Mode)]
        [Toggle(IS_REFLECT)]_Reflect("Reflection", Float) = 0

        [Header(Rotation)]
        _RotationX("RotationX", Range(0, 360)) = 0
        _RotationY("RotationY", Range(0, 360)) = 0
        _RotationZ("RotationZ", Range(0, 360)) = 0

        [Header(SkyBox)]
        [NoScaleOffset] _FrontTex("Front [+Z] (HDR)", 2D) = "grey" {}
        [NoScaleOffset] _BackTex("Back [-Z] (HDR)", 2D) = "grey" {}
        [NoScaleOffset] _LeftTex("Left [+X] (HDR)", 2D) = "grey" {}
        [NoScaleOffset] _RightTex("Right [-X] (HDR)", 2D) = "grey" {}
        [NoScaleOffset] _UpTex("Up [+Y] (HDR)", 2D) = "grey" {}
        [NoScaleOffset] _DownTex("Down [-Y] (HDR)", 2D) = "grey" {}

        [Header(Rendering)]
        _Cutout("Cutout", Range(0,1)) = 0.01
        [Enum(UnityEngine.Rendering.CullMode)]
        _Cull("Cull", Float) = 1                // Front
        [Enum(Off, 0, On, 1)]
        _ZWrite("ZWrite", Float) = 1            // On
        [Enum(UnityEngine.Rendering.BlendMode)]
        _SrcFactor("Src Factor", Float) = 5     // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)]
        _DstFactor("Dst Factor", Float) = 10    // OneMinusSrcAlpha
    }

    SubShader{
        Tags { "Queue" = "Geometry+501" "RenderType" = "Transparent" "IgnoreProjector" = "True"}
        Cull[_Cull]
        ZWrite[_ZWrite]
        Blend[_SrcFactor][_DstFactor]

        Pass {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            #pragma shader_feature IS_REFLECT

            fixed4 _Tint;
            half _Exposure, _Opacity, _Cutout, _RotationX, _RotationY, _RotationZ;

            float radians(float degrees) {
                return degrees * UNITY_PI / 180;
            }

            float2x2 rotateMat(float r) {
                float s = sin(r), c = cos(r);
                return float2x2(c, -s, s, c);
            }

            float2 rotate(float2 p, float r) {
                return mul(rotateMat(r), p);
            }

            float3 RotateX(float3 vertex, float degrees)
            {
                float2 rotatedYZ = rotate(vertex.yz, radians(degrees));
                return float3(rotatedYZ, vertex.x).zxy;
            }

            float3 RotateY(float3 vertex, float degrees)
            {
                float2 rotatedXZ = rotate(vertex.xz, radians(degrees));
                return float3(rotatedXZ, vertex.y).xzy;
            }

            float3 RotateZ(float3 vertex, float degrees)
            {
                float2 rotatedXY = rotate(vertex.xy, radians(degrees));
                return float3(rotatedXY, vertex.z);
            }


            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 viewDir: TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                float3 viewDir = WorldSpaceViewDir(v.vertex);
                viewDir = RotateY(viewDir, _RotationY);
                viewDir = RotateX(viewDir, _RotationX);
                viewDir = RotateZ(viewDir, _RotationZ);
                o.viewDir = viewDir;
                return o;
            }


            float2 remap(float2 uv) {
                return uv * 0.5 + 0.5;
            }

            sampler2D _FrontTex, _BackTex, _LeftTex, _RightTex, _UpTex, _DownTex;
            half4 _FrontTex_HDR, _BackTex_HDR, _LeftTex_HDR, _RightTex_HDR, _UpTex_HDR, _DownTex_HDR;

            float atan2g4(float2 yx) {
                float atn2 = atan2(yx.y, yx.x) / UNITY_PI;
                return floor((atn2 + 1) * 2);
            }

            half3 texHDR(sampler2D smp, half4 smpDecode, float2 uv) {
                return DecodeHDR(tex2D(smp, uv), smpDecode);
            }

            half3 sky6side(float3 dir) {
                float2 yx = rotate(dir.yx, UNITY_PI * 0.75);
                float2 xz = rotate(dir.xz, UNITY_PI * 1.25);
                float2 yz = rotate(dir.yz, UNITY_PI * 1.25);

                fixed radYX = atan2g4(yx);
                fixed radXZ = atan2g4(xz);
                fixed radYZ = atan2g4(yz);

                if (radYX == 0 && radXZ == 0)
                    return texHDR(_LeftTex, _LeftTex_HDR, remap(float2(-dir.z, dir.y) / dir.x));
                else if (radYX == 2 && radXZ == 2)
                    return texHDR(_RightTex, _RightTex_HDR, remap(float2(-dir.z, -dir.y) / dir.x));
                else if (radYX == 3 && radYZ == 0)
                    return texHDR(_UpTex, _UpTex_HDR, remap(float2(dir.x, -dir.z) / dir.y));
                else if (radYX == 1 && radYZ == 2)
                    return texHDR(_DownTex, _DownTex_HDR, remap(float2(-dir.x, -dir.z) / dir.y));
                else if (radYZ == 1)
                    return texHDR(_FrontTex, _FrontTex_HDR, remap(float2(dir.x, dir.y) / dir.z));
                return texHDR(_BackTex, _BackTex_HDR, remap(float2(dir.x, -dir.y) / dir.z));
            }


            half4 frag(v2f i) : SV_Target{
                float a = saturate(_Opacity);
                clip(a - _Cutout - 0.01);

                float3 dir = normalize(-i.viewDir);
            #ifdef IS_REFLECT
                dir.y = abs(dir.y);
            #endif
                half3 c = sky6side(dir);
                c *= _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
                return half4(c, a);
            }
            ENDCG
        }
    }
}