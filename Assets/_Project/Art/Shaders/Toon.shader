// Anime cel shading for characters: a hard light/shadow step (receiving real shadows), a coloured shadow tone, a rim
// light, a small specular glint, and an ink outline drawn as an inverted hull that keeps a steady pixel width.
Shader "HunterAwakening/Toon"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff (0 = opaque)", Range(0, 1)) = 0
        [Header(Cel)]
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.55, 0.72, 1)
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = 0.05
        _ShadowSoftness ("Shadow Softness", Range(0.001, 0.5)) = 0.04
        [Header(Rim and Glint)]
        _RimColor ("Rim Color", Color) = (1, 0.95, 0.9, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.35
        _RimWidth ("Rim Width", Range(0, 1)) = 0.32
        _Glint ("Glint", Range(0, 1)) = 0.12
        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.12, 0.08, 0.1, 1)
        _OutlineWidth ("Outline Width (px at 1080p)", Range(0, 6)) = 1.6
        [HideInInspector] _HitFlash ("Hit Flash", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _ShadowTint, _RimColor, _OutlineColor;
            half _ShadowThreshold, _ShadowSoftness, _RimStrength, _RimWidth, _Glint, _OutlineWidth, _HitFlash, _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Cut-out decals (eyelashes, brows, hair tips): texels below the cutoff are discarded in every pass.
        void ClipAlpha(float2 uv)
        {
            clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a - _Cutoff);
        }
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                clip(albedo.a - _Cutoff);
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = dot(n, sun.direction);
                // One hard step: lit or in shadow. Cast shadows push a surface into the shadow tone too.
                half lit = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, ndl)
                           * smoothstep(0.35, 0.55, sun.shadowAttenuation);
                half3 ambient = SampleSH(n);
                half3 shadowColor = albedo.rgb * _ShadowTint.rgb * (0.55 + 0.6 * ambient);
                half3 litColor = albedo.rgb * (sun.color * 0.85 + ambient * 0.35);
                half3 color = lerp(shadowColor, litColor, lit);

                // Fill from point lights (aura glow, cue lights), stepped like the sun.
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0; li < count; li++)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    half amount = smoothstep(0.0, 0.1, dot(n, l.direction)) * l.distanceAttenuation;
                    color += albedo.rgb * l.color * amount * 0.6;
                }
                #endif

                half fresnel = 1 - saturate(dot(n, v));
                half rim = smoothstep(1 - _RimWidth, 1 - _RimWidth + 0.05, fresnel) * saturate(ndl + 0.3);
                color += _RimColor.rgb * sun.color * rim * _RimStrength;

                float3 h = normalize(sun.direction + v);
                half glint = smoothstep(0.96, 0.975, dot(n, h)) * lit * _Glint;
                color += sun.color * glint;

                // Struck: a white impact frame (set per renderer by HitFlash).
                color = lerp(color, half3(1.0, 0.97, 0.92) * 1.4, _HitFlash);
                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            // Inverted hull: back faces pushed out along the normal, in screen space so the ink line keeps its width.
            Name "ToonOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float4 positionCS = TransformObjectToHClip(v.positionOS.xyz);
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldNormal(v.normalOS));
                float2 offset = normalize(normalCS.xy + 1e-5) * _OutlineWidth * (_ScreenParams.y / 1080.0);
                positionCS.xy += offset / _ScreenParams.xy * 2.0 * positionCS.w;
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Ink takes a little of the surface colour so outlines on light clothes aren't harsh black.
                ClipAlpha(i.uv);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half3 ink = lerp(_OutlineColor.rgb, albedo * 0.25, 0.35);
                return half4(MixFog(ink, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                ClipAlpha(i.uv);
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                ClipAlpha(i.uv);
                return 0;
            }
            ENDHLSL
        }
    }
}
