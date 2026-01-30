Shader "RailPreview/BezierDeform"
{
    Properties
    {
        _MainTex("MainTex", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _PreviewColor("Preview Color", Color) = (0.41349236,0.5979935,0.8679245,1)
        _ScanlineSpeed("Scanline Speed", Float) = 10
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #define _SPECULAR_SETUP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define BEZIER_MAX_SAMPLES 16

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _PreviewColor;
                float4 _MainTex_ST;
                float _ScanlineSpeed;
                float4 _BezierP0;
                float4 _BezierP1;
                float4 _BezierP2;
                float4 _BezierP3;
                float4 _BezierAxisRotation;
                float _BezierCurveLength;
                float _BezierSegmentStart;
                float _BezierSegmentLength;
                float _BezierForwardMin;
                float _BezierMeshLength;
                float _BezierSampleCount;
                float _BezierArcLengths[BEZIER_MAX_SAMPLES + 1];
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            float3 RotateByQuaternion(float3 v, float4 q)
            {
                float3 t = 2.0 * cross(q.xyz, v);
                return v + q.w * t + cross(q.xyz, t);
            }

            float3 BezierPoint(float3 p0, float3 p1, float3 p2, float3 p3, float t)
            {
                float u = 1.0 - saturate(t);
                float tt = t * t;
                float uu = u * u;
                float uuu = uu * u;
                float ttt = tt * t;
                float3 result = uuu * p0;
                result += 3.0 * uu * t * p1;
                result += 3.0 * u * tt * p2;
                result += ttt * p3;
                return result;
            }

            float3 BezierTangent(float3 p0, float3 p1, float3 p2, float3 p3, float t)
            {
                float u = 1.0 - saturate(t);
                float3 term0 = (p1 - p0) * (3.0 * u * u);
                float3 term1 = (p2 - p1) * (6.0 * u * t);
                float3 term2 = (p3 - p2) * (3.0 * t * t);
                return term0 + term1 + term2;
            }

            float DistanceToTime(float distance)
            {
                if (_BezierCurveLength <= 1e-5) return 0.0;
                float dist = clamp(distance, 0.0, _BezierCurveLength);
                int steps = (int)clamp(_BezierSampleCount, 1.0, (float)BEZIER_MAX_SAMPLES);
                float stepSize = 1.0 / steps;
                float prev = _BezierArcLengths[0];

                [loop]
                for (int i = 1; i <= steps; i++)
                {
                    float current = _BezierArcLengths[i];
                    if (dist > current)
                    {
                        prev = current;
                        continue;
                    }

                    float lerpValue = abs(current - prev) <= 1e-5 ? 0.0 : (dist - prev) / (current - prev);
                    return (i - 1) * stepSize + lerpValue * stepSize;
                }

                return 1.0;
            }

            void BuildCurveFrame(float3 tangent, out float3 right, out float3 up, out float3 forward)
            {
                forward = dot(tangent, tangent) > 1e-6 ? normalize(tangent) : float3(0.0, 0.0, 1.0);
                float3 horizontal = float3(forward.x, 0.0, forward.z);
                float horizontalSqr = dot(horizontal, horizontal);

                float3 yawForward = horizontalSqr > 1e-6 ? normalize(horizontal) : float3(0.0, 0.0, 1.0);
                float3 yawRight = normalize(cross(float3(0.0, 1.0, 0.0), yawForward));
                float3 yawUp = cross(yawForward, yawRight);

                if (horizontalSqr <= 1e-6)
                {
                    yawRight = float3(1.0, 0.0, 0.0);
                    yawUp = float3(0.0, 1.0, 0.0);
                    yawForward = float3(0.0, 0.0, 1.0);
                }

                float localY = dot(forward, yawUp);
                float localZ = dot(forward, yawForward);
                float pitch = atan2(localY, max(1e-6, localZ));
                float s = sin(pitch);
                float c = cos(pitch);

                right = yawRight;
                forward = yawForward * c + yawUp * s;
                up = yawUp * c - yawForward * s;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 aligned = RotateByQuaternion(input.positionOS.xyz, _BezierAxisRotation);
                float meshLength = max(_BezierMeshLength, 1e-5);
                float normalizedForward = (aligned.z - _BezierForwardMin) / meshLength;
                normalizedForward = saturate(normalizedForward);

                float startDistance = clamp(_BezierSegmentStart, 0.0, _BezierCurveLength);
                float usableLength = max(_BezierSegmentLength, 1e-5);
                float distanceOnCurve = startDistance + normalizedForward * usableLength;

                float t = DistanceToTime(distanceOnCurve);
                float3 curvePos = BezierPoint(_BezierP0.xyz, _BezierP1.xyz, _BezierP2.xyz, _BezierP3.xyz, t);
                float3 tangent = BezierTangent(_BezierP0.xyz, _BezierP1.xyz, _BezierP2.xyz, _BezierP3.xyz, t);

                float3 right;
                float3 up;
                float3 forward;
                BuildCurveFrame(tangent, right, up, forward);

                float3 offset = right * aligned.x + up * aligned.y;
                float3 deformedPos = curvePos + offset;

                output.positionCS = TransformObjectToHClip(deformedPos);
                output.positionWS = TransformObjectToWorld(deformedPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 viewPos = TransformWorldToView(input.positionWS);
                float scanlinePhase = viewPos.y * 60.0 + _TimeParameters.x * _ScanlineSpeed;
                float scanline = sin(scanlinePhase) + 1.34;
                float4 baseSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 tinted = baseSample * _Color;
                float4 shaded = tinted * scanline * _PreviewColor;
                return half4(shaded.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
