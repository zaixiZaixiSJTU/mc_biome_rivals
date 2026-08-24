Shader "BiomeRivals/Demo/GroundSurface"
{
    Properties
    {
        _MainTex ("Projected Ground", 2D) = "white" {}
        _Color ("Base Color", Color) = (1, 1, 1, 1)
        _HighlightColor ("Highlight Color", Color) = (0, 0, 0, 1)
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0
        _EdgeWidth ("Pixel Edge Width", Range(0.01, 0.25)) = 0.09
    }

    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
        LOD 100
        ZWrite On
        Cull Back
        Offset -1, -1

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
                float2 cellUv : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 cellUv : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _HighlightColor;
            float _HighlightStrength;
            float _EdgeWidth;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.cellUv = input.cellUv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 ground = tex2D(_MainTex, input.uv) * _Color;
                float edgeDistance = min(min(input.cellUv.x, 1.0 - input.cellUv.x), min(input.cellUv.y, 1.0 - input.cellUv.y));
                float edge = 1.0 - smoothstep(_EdgeWidth, _EdgeWidth * 2.0, edgeDistance);
                float strength = saturate(_HighlightStrength);
                float surfaceMask = strength * lerp(0.84, 1.0, edge);
                fixed3 activated = ground.rgb * (1.0 + strength * 0.18) + _HighlightColor.rgb * (0.30 + edge * 0.12);
                return fixed4(lerp(ground.rgb, activated, surfaceMask), 1.0);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Texture"
}
