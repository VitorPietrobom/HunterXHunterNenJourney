// Aura flowing off the body: drawn on a copy of the character's skinned mesh, pushed out along the normals.
// Noise scrolling upward in world space raises flame tongues and breaks the rim glow into licks of aura.
Shader "HunterAwakening/AuraShell"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.55, 0.15, 1)
        _Intensity ("Intensity", Float) = 2
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Thickness ("Shell Thickness (m)", Float) = 0.02
        _FlameHeight ("Flame Height (m)", Float) = 0.05
        _RimPower ("Rim Power", Float) = 2
        _NoiseScale ("Noise Scale", Float) = 7
        _ScrollSpeed ("Rise Speed", Float) = 1.5
        _Turbulence ("Turbulence", Range(0, 1)) = 0.6
        _Flash ("Flash", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "AuraShell"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity, _Alpha, _Thickness, _FlameHeight, _RimPower, _NoiseScale, _ScrollSpeed, _Turbulence, _Flash;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float tongue : TEXCOORD2; };

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Fbm(float3 p) { return 0.55 * Noise(p) + 0.3 * Noise(p * 2.13) + 0.15 * Noise(p * 4.37); }

            // Flames rise: the noise field scrolls upward and is stretched vertically.
            float Flow(float3 positionWS, float scale, float speed)
            {
                float3 p = positionWS * float3(scale, scale * 0.55, scale);
                p.y -= _Time.y * speed;
                return Fbm(p);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float n = Flow(positionWS, _NoiseScale, _ScrollSpeed);
                float tongue = saturate((n - 0.35) * 2.2);
                // Tongues grow from the top and sides of each limb, not from surfaces facing the ground.
                float facing = saturate(normalWS.y * 0.6 + 0.7);
                positionWS += normalWS * (_Thickness + _FlameHeight * tongue * facing);
                positionWS.y += _FlameHeight * 1.6 * tongue * tongue;
                o.positionWS = positionWS;
                o.normalWS = normalWS;
                o.tongue = tongue;
                o.positionCS = TransformWorldToHClip(positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float facing = abs(dot(normalize(i.normalWS), viewDir));
                float rim = pow(1.0 - facing, _RimPower);
                float n = Flow(i.positionWS, _NoiseScale * 1.8, _ScrollSpeed * 1.7);
                // A faint even veil over the body, a strong broken rim, and bright tongues where the noise peaks.
                float flame = 0.015 + rim * lerp(1.0, saturate(n * 1.8 - 0.25), _Turbulence) * 1.3 + i.tongue * rim * 0.8;
                flame = saturate(flame) * _Alpha;
                // White-hot where the aura is densest.
                float3 color = lerp(_Color.rgb, float3(1, 1, 1), saturate(flame * flame * 0.5 + _Flash * 0.5));
                return half4(color * flame * (_Intensity + _Flash * 3.0), 1);
            }
            ENDHLSL
        }
    }
}
