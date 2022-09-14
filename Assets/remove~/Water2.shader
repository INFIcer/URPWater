Shader "Unlit/Water"
{
    Properties
    {
        _BumpScale("Detail Wave Amount", Range(0, 2)) = 0.2//fine detail multiplier
        _DitherPattern ("Dithering Pattern", 2D) = "bump" {}
        [Toggle(_STATIC_SHADER)] _Static ("Static", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" "Queue"="Transparent-100" "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode"="UniversalForward"
            }

            HLSLPROGRAM
            #pragma target 4.5

            #pragma multi_compile _ USE_STRUCTURED_BUFFER

            #include "WaterCommon.hlsl"

            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            ENDHLSL
        }
    }
}