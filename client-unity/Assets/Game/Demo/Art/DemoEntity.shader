Shader "BiomeRivals/Demo/Entity"
{
    Properties
    {
        _MainTex ("Entity Texture", 2D) = "white" {}
        _SurfaceOverlayTex ("Biome Surface (same UV)", 2D) = "black" {}
        _UseSurfaceOverlay ("Use Biome Surface", Float) = 0
        _UseLowAlphaEmission ("Low Alpha Eye Emission", Float) = 0
        _UseAlphaColorMask ("Alpha Is Dye Mask", Float) = 0
        _MaskColor ("Masked Wool Color", Color) = (1, 1, 1, 1)
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
            sampler2D _SurfaceOverlayTex;
            float _UseSurfaceOverlay;
            float _UseLowAlphaEmission;
            float _UseAlphaColorMask;
            fixed4 _MaskColor;
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
                if (_UseSurfaceOverlay > 0.5)
                {
                    fixed4 surface = tex2D(_SurfaceOverlayTex, input.uv);
                    fixed alpha = surface.a + tex.a * (1.0 - surface.a);
                    tex.rgb = (surface.rgb * surface.a + tex.rgb * tex.a * (1.0 - surface.a)) / max(alpha, 0.0001);
                    tex.a = alpha;
                }
                // Classic Minecraft entity look: texture x per-face vertex shading x tint,
                // plus an optional unshaded emissive lift for fire creatures.
                fixed3 lit = tex.rgb * input.color.rgb * _Color.rgb;
                // Registered sheep alpha encodes dye weighting, not opacity or emission.
                if (_UseAlphaColorMask > 0.5)
                    lit *= lerp(fixed3(1, 1, 1), _MaskColor.rgb, tex.a);
                // Opt-in adapter for registered Bedrock eye pixels; a=0 still clips below.
                if (_UseLowAlphaEmission > 0.5)
                    lit = lerp(lit, tex.rgb * _Color.rgb, 1.0 - tex.a);
                fixed3 rgb = lit + tex.rgb * _EmissiveBoost;
                if (_UseAlphaColorMask < 0.5) clip(tex.a * _Color.a - _Cutoff);
                fixed4 result = fixed4(rgb, 1.0);
                UNITY_APPLY_FOG(input.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }

    FallBack "Transparent/Cutout/VertexLit"
}
