Shader "Hidden/SplatoonInkPrototype/InkMetaballComposite"
{
    Properties
    {
        _MainTex ("Blurred Ink", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.84, 0.86, 0.9, 1)
        _BaseShadowTint ("Base Shadow Tint", Color) = (0.66, 0.7, 0.76, 1)
        _TileScale ("Tile Scale", Float) = 6
        _GroutStrength ("Grout Strength", Float) = 0.22
        _InkBoost ("Ink Boost", Float) = 1.08
        _Threshold ("Threshold", Float) = 0.2
        _Feather ("Feather", Float) = 0.32
        _EdgeDarkening ("Edge Darkening", Float) = 0.08
        _HighlightStrength ("Highlight Strength", Float) = 0.26
        _HighlightScale ("Highlight Scale", Float) = 20
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _BaseColor;
            float4 _BaseShadowTint;
            float _TileScale;
            float _GroutStrength;
            float _InkBoost;
            float _Threshold;
            float _Feather;
            float _EdgeDarkening;
            float _HighlightStrength;
            float _HighlightScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 grid = abs(frac(i.uv * _TileScale) - 0.5);
                float grout = 1.0 - smoothstep(0.44, 0.5, min(grid.x, grid.y) * 2.0);
                float3 baseColor = lerp(_BaseShadowTint.rgb, _BaseColor.rgb, 0.78);
                baseColor = lerp(baseColor, _BaseShadowTint.rgb, grout * _GroutStrength);

                fixed4 ink = tex2D(_MainTex, i.uv);
                float field = saturate(ink.a);
                float feather = max(0.001, _Feather);
                float coverage = smoothstep(_Threshold, _Threshold + feather, field);
                float inner = smoothstep(_Threshold + feather * 0.42, _Threshold + feather, field);
                float edge = saturate(coverage - inner);

                float3 inkColor = saturate(ink.rgb * _InkBoost);
                inkColor = lerp(inkColor, inkColor * (1.0 - _EdgeDarkening), edge);

                float streak = frac(sin(dot(i.uv * float2(_HighlightScale, _HighlightScale * 0.37), float2(12.9898, 78.233))) * 43758.5453);
                streak = smoothstep(0.88, 1.0, streak);
                float broad = smoothstep(0.35, 0.95, coverage) * (1.0 - edge * 0.55);
                float highlight = saturate((streak * 0.18 + broad * 0.16) * _HighlightStrength * coverage);
                inkColor = lerp(inkColor, 1.0.xxx, highlight);

                float3 color = lerp(baseColor, inkColor, coverage);
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
}
