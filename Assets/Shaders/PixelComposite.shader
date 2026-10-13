// 【픽셀 캐릭터 합성】 (결정 2-115) — 저해상도로 그린 캐릭터 그림(PixelArtView)을 화면에 얹는다.
//   · 점 필터로 키워 도트처럼 보이게, 알파는 0/1로 끊어 가장자리를 또렷하게
//   · 비어 있는 칸 옆에 캐릭터가 있으면 1칸 외곽선을 그린다
//   · 색을 몇 단계로 끊어(포스터라이즈) 도트 그림의 적은 색 느낌을 낸다
Shader "Dokkaebi/PixelComposite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _OutlineColor ("Outline", Color) = (0.06, 0.05, 0.08, 1)
        _Levels ("Color Levels", Float) = 8
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _OutlineColor;
            float _Levels;
            float _Cutoff;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 칸 한가운데를 읽는다 — 점 필터와 같다.
                float2 texel = _MainTex_TexelSize.xy;
                float2 uv = (floor(i.uv / texel) + 0.5) * texel;
                fixed4 c = tex2D(_MainTex, uv);

                if (c.a >= _Cutoff)
                {
                    float levels = max(2.0, _Levels);
                    c.rgb = floor(c.rgb * levels + 0.5) / levels;
                    c.a = 1;
                    return c * i.color;
                }

                float n = tex2D(_MainTex, uv + float2(texel.x, 0)).a
                        + tex2D(_MainTex, uv - float2(texel.x, 0)).a
                        + tex2D(_MainTex, uv + float2(0, texel.y)).a
                        + tex2D(_MainTex, uv - float2(0, texel.y)).a;
                if (n >= _Cutoff)
                    return fixed4(_OutlineColor.rgb, _OutlineColor.a * i.color.a);

                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}
