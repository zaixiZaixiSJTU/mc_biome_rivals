Shader "BiomeRivals/Demo/CompositeBackdrop"
{
    Properties
    {
        _PlayerTex ("Player Half", 2D) = "white" {}
        _OpponentTex ("Opponent Half", 2D) = "white" {}
        _NeutralTex ("Neutral Center", 2D) = "white" {}
        _PlayerTint ("Player Tint", Color) = (1, 1, 1, 1)
        _OpponentTint ("Opponent Tint", Color) = (1, 1, 1, 1)
        _CenterHalfWidth ("Neutral Center Half Width", Range(0.01, 0.15)) = 0.035
        _BlendWidth ("Seam Blend Width", Range(0.001, 0.08)) = 0.018
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" }
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _PlayerTex;
            sampler2D _OpponentTex;
            sampler2D _NeutralTex;
            fixed4 _PlayerTint;
            fixed4 _OpponentTint;
            float _CenterHalfWidth;
            float _BlendWidth;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.uv;
                fixed4 player = tex2D(_PlayerTex, uv) * _PlayerTint;
                fixed4 opponent = tex2D(_OpponentTex, uv) * _OpponentTint;
                fixed4 neutral = tex2D(_NeutralTex, uv);
                float signedDistance = uv.y - 0.5;
                float centerMask = 1.0 - smoothstep(_CenterHalfWidth, _CenterHalfWidth + _BlendWidth, abs(signedDistance));
                fixed4 side = lerp(player, opponent, step(0.0, signedDistance));
                return lerp(side, neutral, centerMask);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Texture"
}
