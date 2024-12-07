Shader "Unlit/DebugPlane"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1mごとに白と灰色が切り替わるチェック柄
                fixed4 col;
                int x = floor(i.worldPos.x);
                int z = floor(i.worldPos.z);
                if ((x + z) % 2 == 0)
                    col = fixed4(1, 1, 1, 1); // 白色
                else
                    col = fixed4(0.5, 0.5, 0.5, 1); // 灰色
                return col;
            }
            ENDCG
        }
    }
}