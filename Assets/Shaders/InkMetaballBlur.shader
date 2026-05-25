Shader "Hidden/SplatoonInkPrototype/InkMetaballBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurDirection ("Blur Direction", Vector) = (1, 0, 0, 0)
        _BlurRadius ("Blur Radius", Float) = 3
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
            float4 _MainTex_TexelSize;
            float4 _BlurDirection;
            float _BlurRadius;

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
                float2 stepUv = _BlurDirection.xy * _MainTex_TexelSize.xy * _BlurRadius;
                fixed4 c = tex2D(_MainTex, i.uv) * 0.227027;
                c += tex2D(_MainTex, i.uv + stepUv * 1.384615) * 0.316216;
                c += tex2D(_MainTex, i.uv - stepUv * 1.384615) * 0.316216;
                c += tex2D(_MainTex, i.uv + stepUv * 3.230769) * 0.070270;
                c += tex2D(_MainTex, i.uv - stepUv * 3.230769) * 0.070270;
                return c;
            }
            ENDCG
        }
    }
}
