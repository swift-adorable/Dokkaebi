// 【가장자리가 번지는 UI 그림】 (결정 2-121) — 전체 지도의 사진 · 덮개 · 안개가 쓴다.
// 그림의 네 변 쪽 _Feather(0 ~ 0.5, uv 비율)만큼 알파가 0으로 사라진다 — 뒤의 흐린 게임 화면 위로 지도가 떠 보이게.
// UI/Default와 같은 스텐실 · 사각 자르기(RectMask2D)를 지킨다. Resources에 둬 빌드에 들어가게 한다(Shader.Find).
Shader "Dokkaebi/UI/SoftEdge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Feather ("Feather", Range(0, 0.5)) = 0.08

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _Feather;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                float2 uv = i.texcoord;
                float2 edge = min(uv, 1.0 - uv);
                float f = max(_Feather, 0.0001);
                float k = smoothstep(0.0, f, edge.x) * smoothstep(0.0, f, edge.y);
                c.a *= k;

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                return c;
            }
            ENDCG
        }
    }
}
