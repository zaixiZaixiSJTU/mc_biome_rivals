Shader "BiomeRivals/Demo/Entity"
{
    Properties
    {
        _MainTex ("Entity Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.1
        _EmissiveBoost ("Emissive Boost", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        ZWrite On
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_FOG_COORDS(1)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed _Cutoff;
            fixed _EmissiveBoost;

            v2f vert(appdata input)
            {
                v2f output;
                output.pos = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                UNITY_TRANSFER_FOG(output, output.pos);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, input.uv);
                // Classic Minecraft entity look: texture x per-face vertex shading x tint,
                // plus an optional unshaded emissive lift for fire creatures.
                fixed3 lit = tex.rgb * input.color.rgb * _Color.rgb;
                fixed3 rgb = lit + tex.rgb * _EmissiveBoost;
                clip(tex.a * _Color.a - _Cutoff);
                fixed4 result = fixed4(rgb, 1.0);
                UNITY_APPLY_FOG(input.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }

    FallBack "Transparent/Cutout/VertexLit"
}
