﻿Shader "Hidden/kMotion/MotionBlur"
{
    Properties
    {
        _MainTex ("Texture", any) = "" {}
    }

    HLSLINCLUDE

    // -------------------------------------
    // Includes
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

    // -------------------------------------
    // Inputs
    TEXTURE2D_X(_MainTex);
    TEXTURE2D_X(_MotionVectorTexture);       SAMPLER(sampler_MotionVectorTexture);

    float _Intensity;
    float4 _MainTex_TexelSize;

    // -------------------------------------
    // Structs
    struct VaryingsMB
    {
        float4 positionCS    : SV_POSITION;
        float4 uv            : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };
    
    // -------------------------------------
    // Vertex
    VaryingsMB VertMB(Attributes input)
    {
        VaryingsMB output;
        
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.positionCS = float4(input.positionOS.xyz, 1);

        #if UNITY_UV_STARTS_AT_TOP
        output.positionCS.y *= -1;
        #endif

        output.uv.xy = input.uv;

        return output;
    }

    // -------------------------------------
    // Fragment
    half4 GatherSample(float sampleNumber, float2 velocity, float invSampleCount, float2 centerUV, float randomVal, float velocitySign)
    {
        float  offsetLength = (sampleNumber + 0.5) + (velocitySign * (randomVal - 0.5));
        float2 sampleUV = centerUV + (offsetLength * invSampleCount) * velocity * velocitySign;
        return SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, sampleUV);
    }

    half4 DoMotionBlur(VaryingsMB input, int iterations)
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv.xy);
        
        float2 velocity = SAMPLE_TEXTURE2D_X(_MotionVectorTexture, sampler_MotionVectorTexture, uv).rg * _Intensity;

        // zCubed: Multiply by 1000 because this noise algorithm causes banding otherwise :/
        // zCubed: Quite annoying that this noise pattern causes such harsh banding...
        float randomVal = InterleavedGradientNoise(uv * 1000.0 * _MainTex_TexelSize.zw, 0);
        float invSampleCount = rcp(iterations * 2);

        half4 color = 0.0;

        UNITY_UNROLL
        for (int i = 0; i < iterations; i++)
        {
            color += GatherSample(i, velocity, invSampleCount, uv, randomVal, -1.0);
            color += GatherSample(i, velocity, invSampleCount, uv, randomVal, 1.0);
        }

        //return abs(SAMPLE_TEXTURE2D_X(_MotionVectorTexture, sampler_MotionVectorTexture, uv));
        //return float4(abs(velocity), 0, 1);
        //return fac;

        return color * invSampleCount;
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Camera Motion Blur - Low Quality"

            HLSLPROGRAM

            #pragma vertex VertMB
            #pragma fragment Frag

            half4 Frag(VaryingsMB input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return DoMotionBlur(input, 2);
            }

            ENDHLSL
        }

        Pass
        {
            Name "Camera Motion Blur - Medium Quality"

            HLSLPROGRAM

            #pragma vertex VertMB
            #pragma fragment Frag

            half4 Frag(VaryingsMB input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return DoMotionBlur(input, 3);
            }

            ENDHLSL
        }

        Pass
        {
            Name "Camera Motion Blur - High Quality"

            HLSLPROGRAM

            #pragma vertex VertMB
            #pragma fragment Frag

            half4 Frag(VaryingsMB input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return DoMotionBlur(input, 4);
            }

            ENDHLSL
        }
    }
}
