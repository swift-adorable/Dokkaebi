// 【가려진 주인공 둘레만 비쳐 보이기】 (v8 · 2026-11-19 사용자 「캐릭터가 벽이나 어떤 객체 뒤로 시야 상 가리게 되면
// 그 객체에 의해 가려진 캐릭터 주변만 그 객체를 약간 투명하게」)
// URP Lit 대신 쓰는 단순 조명 셰이더 — 해(주 광원) · 그림자 · 주변광(SH) · 보조 광원 · 안개.
// 화면에서 주인공 둘레 원 안이고 주인공보다 카메라에 가까운 면이면 디더(점 무늬)로 일부를 비운다 → 반투명처럼 보인다.
// 전역 값: _SeeThroughPos (xyz = 주인공 가슴 높이, w = 켜짐) · _SeeThroughRadius (화면 높이 대비 반지름) — SeeThrough.cs 가 매 프레임 넣는다.
Shader "Dokkaebi/SeeThroughLit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0
        _Cull ("Cull", Float) = 2
        _SeeThrough ("See Through (1 = 주인공을 가리면 비침)", Float) = 1
        _FlatDepth ("Flat Depth (1 = 판 전체를 발 위치 깊이로)", Float) = 0
        _Fade ("Fade (적이 시야에 들어오고 나갈 때 0 ~ 1)", Float) = 1
        [HideInInspector] _MainTex ("MainTex", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _Cull;
            half _SeeThrough;
            half _FlatDepth;
            half _Fade;
        CBUFFER_END

        // 캐릭터 · 몬스터 판 — 카메라 쪽으로 눕힌 판의 윗부분이 뒤 벽 속으로 파고들어 벽 앞에 서도 가려졌다(v8c 사용자 2번).
        // 판 전체의 깊이를 발 자리(물체 원점) 깊이로 맞춘다 — 앞뒤는 발 위치로만 가린다.
        // 【서 있는 사람의 깊이로】 (v8e · 2026-11-20 폰 「몬스터가 여전히 가려지듯이」) — 그림판은 카메라를 마주 보게 기울어 있어
        // 판 전체가 발과 같은 깊이였다. 그래서 바로 뒤의 담 · 벽 윗부분이 몸통을 덮었다. 판 위 한 점을 「발에서 그만큼 높이 곧게 선
        // 사람의 같은 화면 자리」 깊이로 그린다: 판 위로 u 만큼이면 높이 u / up.y (기운 판이 화면에서 짧아진 만큼 되돌림).
        // 바닥 0.45m 는 기단 · 방바닥(0.3m) 윗면이 발을 덮지 않게 (v8d).
        float4 FlattenDepth(float4 positionCS, float3 positionWS)
        {
            if (_FlatDepth < 0.5) return positionCS;
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            float3 up = TransformObjectToWorldDir(float3(0, 1, 0));
            float u = max(0.0, dot(positionWS - origin, up));
            float h = 0.45 + u / max(0.3, up.y);
            float4 anchor = TransformWorldToHClip(origin + float3(0, h, 0));
            positionCS.z = anchor.z / anchor.w * positionCS.w;
            return positionCS;
        }

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // 【덕코프식 시야】 (2026-11-23 v3) — PlayerVision.cs 가 갈래(360개)마다 보이는 거리를 한 줄 그림 _VisionRays 로 넣는다.
        // 화면의 점마다 주인공(_VisionCenter.xy)에게서의 각도 · 거리를 구해 그 갈래 거리와 견준다 — 경계를 화소 폭(fwidth)으로 흐려 계단이 없다.
        // _VisionCenter = (눈 x, 눈 z, 켜짐, 갈래 수). 보이는 곳은 _VisionLit 배 밝게, 보이지 않는 곳은 _VisionDark · _VisionGray.
        TEXTURE2D(_VisionRays);
        SAMPLER(sampler_VisionRays);
        float4 _VisionCenter;
        float _VisionDark;
        float _VisionGray;
        float _VisionLit;

        half3 ApplyVision(half3 c, float3 positionWS)
        {
            if (_VisionCenter.z < 0.5) return c;
            float2 d = positionWS.xz - _VisionCenter.xy;
            float dist = length(d);
            float u = frac(atan2(d.x, d.y) * 0.15915494) + 0.5 / max(_VisionCenter.w, 1.0);   // 북쪽 0 · 시계 방향, 갈래 칸 가운데
            float l = SAMPLE_TEXTURE2D_LOD(_VisionRays, sampler_VisionRays, float2(u, 0.5), 0).r;
            float e = l - dist;
            half m = saturate(e / max(fwidth(e) * 1.5, 0.02) + 0.5);
            half3 gray = dot(c, half3(0.3, 0.59, 0.11));
            half3 outside = lerp(c, gray, _VisionGray) * _VisionDark;
            return lerp(outside, c * _VisionLit, m);
        }

        float4 _SeeThroughPos;
        float _SeeThroughRadius;

        float Bayer4(float2 p)
        {
            uint2 q = uint2(p) % 4;
            const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            return (m[q.y * 4 + q.x] + 0.5) / 16.0;
        }

        // 주인공 둘레 원 안 · 주인공보다 앞(카메라 쪽)인 조각을 점 무늬로 비운다. 가운데일수록 많이 비운다.
        void SeeThroughClip(float4 pixelPos, float3 positionWS)
        {
            if (_SeeThroughPos.w < 0.5 || _SeeThrough < 0.5) return;
            float4 p = TransformWorldToHClip(_SeeThroughPos.xyz);
            float4 f = TransformWorldToHClip(positionWS);
            float2 d = f.xy / f.w - p.xy / p.w;
            d.x *= _ScreenParams.x / _ScreenParams.y;
            float r = length(d) / max(0.001, _SeeThroughRadius * 2.0);
            if (r >= 1.0) return;
            float camToFrag = distance(positionWS, _WorldSpaceCameraPos);
            float camToPlayer = distance(_SeeThroughPos.xyz, _WorldSpaceCameraPos);
            if (camToFrag > camToPlayer - 0.8) return;
            float keep = lerp(0.3, 1.0, smoothstep(0.55, 1.0, r));   // 가운데는 30% 만 남긴다
            clip(keep - Bayer4(pixelPos.xy) - 0.001);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _LIGHT_LAYERS
            #define _SPECULARHIGHLIGHTS_OFF 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = FlattenDepth(p.positionCS, p.positionWS);
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                clip(albedo.a - _Cutoff - 0.0001);
                if (_Fade < 0.999) clip(_Fade - Bayer4(i.positionCS.xy));   // 시야 페이드 — 점 무늬로 서서히 (PlayerVision)
                SeeThroughClip(i.positionCS, i.positionWS);

                float3 n = normalize(i.normalWS);   // 뒤집지 않는다 — 집 상자 면은 감긴 방향이 거꾸로라 뒤집으면 윗면 바닥이 아래를 봐 새까맸다 (v8b · URP Lit 도 안 뒤집음)
                // URP Lit 과 같은 조명 함수 그대로 (v8b — 손으로 짠 조명은 밤에 2 ~ 3배 어두웠다: 보조 해 · 환경광이 빠짐)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fog;
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.metallic = 0;
                surface.specular = half3(0, 0, 0);
                surface.smoothness = 0;
                surface.occlusion = 1;
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, i.fog);
                color.rgb = ApplyVision(color.rgb, i.positionWS);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float3 nw = TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - ws);
                #else
                    float3 ld = _LightDirection;
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(ws, nw, ld));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;
                clip(a - _Cutoff - 0.0001);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = FlattenDepth(TransformWorldToHClip(o.positionWS), o.positionWS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;
                clip(a - _Cutoff - 0.0001);
                SeeThroughClip(i.positionCS, i.positionWS);
                return 0;
            }
            ENDHLSL
        }

        // 깊이 · 노멀 — PC 렌더러는 이걸로 화면 그림자 · 주변 가림(SSAO)을 만든다. 없으면 바닥 아래 땅 깊이로 그림자를 재서 새까맣게 나왔다(v8b).
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = FlattenDepth(TransformWorldToHClip(o.positionWS), o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;
                clip(a - _Cutoff - 0.0001);
                SeeThroughClip(i.positionCS, i.positionWS);
                float3 n = normalize(i.normalWS);   // 뒤집지 않는다 — 집 상자 면은 감긴 방향이 거꾸로라 뒤집으면 윗면 바닥이 아래를 봐 새까맸다 (v8b · URP Lit 도 안 뒤집음)
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
                #else
                    return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
