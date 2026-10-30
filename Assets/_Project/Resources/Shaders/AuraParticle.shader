// Additive soft particle for aura flames: a round falloff with a white-hot core, no texture needed.
Shader "HunterAwakening/AuraParticle"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 1.6
        _CoreHeat ("Core Heat", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "AuraParticle"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity, _CoreHeat;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                float r = saturate(1.0 - dot(d, d));
                float glow = r * r * r; // soft edges so neighbouring particles blend
                float core = glow * glow * glow;
                float3 color = (i.color.rgb + core * _CoreHeat) * glow * i.color.a * _Intensity;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
