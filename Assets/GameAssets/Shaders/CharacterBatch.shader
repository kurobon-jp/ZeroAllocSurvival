Shader "ZeroAllocSurvival/Character BRG"
{
    Properties
    {
        _MainTex("Character Atlas", 2D) = "white" {}
        _EmissionColor("Emission Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _AtlasSize("Atlas Size", Vector) = (1, 1, 1, 1)
        [HideInInspector] _VisualData("Visual Data", Vector) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite On
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "CharacterBatch"
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _EmissionColor;
                float4 _AtlasSize;
                float4 _VisualData;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float4, _VisualData)
                UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            #endif

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 effect : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                #ifdef UNITY_DOTS_INSTANCING_ENABLED
                    float4 visual = UNITY_ACCESS_DOTS_INSTANCED_PROP(float4, _VisualData);
                #else
                    float4 visual = _VisualData;
                #endif
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                float2 localUv = input.uv;
                if (visual.y > .5) localUv.x = 1.0 - localUv.x;
                float column = fmod(visual.x, _AtlasSize.x);
                float rowFromTop = floor(visual.x / _AtlasSize.x);
                output.uv = (localUv + float2(column, _AtlasSize.y - 1.0 - rowFromTop)) * _AtlasSize.zw;
                output.effect = visual.zw;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(color.a - 0.01h);
                // Depth-writing sprites cannot use ordinary alpha fade without hiding characters
                // behind an almost transparent quad. Stochastic coverage preserves the perceived
                // fade while only surviving pixels write depth.
                float dither = frac(52.9829189 * frac(dot(floor(input.positionCS.xy),
                    float2(0.06711056, 0.00583715))));
                clip(input.effect.y - dither);
                color.a *= input.effect.y;
                color.rgb += _EmissionColor.rgb * input.effect.x * color.a;
                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }
}
