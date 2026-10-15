// 【가려진 캐릭터 실루엣】 (결정 2-130) — 나무 · 집 · 담 뒤에 들어간 캐릭터를 반투명 실루엣으로 보인다.
// 앞에 무엇이 있을 때(깊이가 더 멀 때 · ZTest Greater)만 그리므로, 보이는 동안에는 아무것도 덧그리지 않는다.
Shader "Dokkaebi/CharacterBehind"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _Color ("Tint (a = 비침)", Color) = (0.25, 0.2, 0.35, 0.75)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Behind"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZTest Greater
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color;
                half _Cutoff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(c.a - _Cutoff);
                // 제 색을 조금 어둡게 — 눈 덮인 흰 나무 위에서도, 짙은 숲 위에서도 보이게.
                return half4(lerp(c.rgb, _Color.rgb, 0.3h), _Color.a);
            }
            ENDHLSL
        }
    }
}
