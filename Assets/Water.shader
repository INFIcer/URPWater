Shader "Unlit/Water"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
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
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment

            #pragma multi_compile _ USE_STRUCTURED_BUFFER
            // make fog work
            //#pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GerstnerWaves.hlsl"

            CBUFFER_START(UnityPerMaterial)
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_END

            struct WaterVertexInput // vert struct
            {
                float4 vertex : POSITION; // vertex positions
                float2 texcoord : TEXCOORD0; // local UVs
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct WaterVertexOutput // fragment struct
            {
                float4 uv : TEXCOORD0; // Geometric UVs stored in xy, and world(pre-waves) in zw
                float3 posWS : TEXCOORD1; // world position of the vertices
                half3 normal : NORMAL; // vert normals
                float3 viewDir : TEXCOORD2; // view direction
                float3 preWaveSP : TEXCOORD3; // screen position of the verticies before wave distortion
                half2 fogFactorNoise : TEXCOORD4; // x: fogFactor, y: noise
                float4 additionalData : TEXCOORD5;
                // x = distance to surface, y = local position in camera space, z = normalized wave height, w = horizontal movement
                half4 shadowCoord : TEXCOORD6; // for ssshadows

                float4 clipPos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 AdditionalData(float3 postionWS, WaveStruct wave)
            {
                half4 data = half4(0.0, 0.0, 0.0, 0.0);
                float3 viewPos = TransformWorldToView(postionWS);
                data.x = length(viewPos / viewPos.z); // distance to surface
                data.y = length(GetCameraPositionWS().xyz - postionWS); // local position in camera space
                //
                float _MaxWaveHeight = 0.5;
                data.z = wave.position.y / _MaxWaveHeight * 0.5 + 0.5;
                // encode the normalized wave height into additional data
                data.w = wave.position.x + wave.position.z;
                return data;
            }

            WaterVertexOutput WaveVertexOperations(WaterVertexOutput input)
            {
                #ifdef _STATIC_SHADER
	float time = 0;
                #else
                float time = _Time.y;
                #endif

                input.normal = float3(0, 1, 0);
                //input.fogFactorNoise.y = ((noise((input.posWS.xz * 0.5) + time) + noise((input.posWS.xz * 1) + time)) * 0.25 - 0.5) + 1;

                // Detail UVs
                input.uv.zw = input.posWS.xz * 0.1h + time * 0.05h + (input.fogFactorNoise.y * 0.1);
                input.uv.xy = input.posWS.xz * 0.4h - time.xx * 0.1h + (input.fogFactorNoise.y * 0.2);

                half4 screenUV = ComputeScreenPos(TransformWorldToHClip(input.posWS));
                screenUV.xyz /= screenUV.w;

                // shallows mask
                half waterDepth = 1; // WaterTextureDepth(input.posWS);
                input.posWS.y += pow(saturate((-waterDepth + 1.5) * 0.4), 2);

                //Gerstner here
                WaveStruct wave;
                SampleWaves(input.posWS, saturate((waterDepth * 0.1 + 0.05)), wave);
                input.normal = wave.normal;
                input.posWS += wave.position;

                #ifdef SHADER_API_PS4
	input.posWS.y -= 0.5;
                #endif

                // Dynamic displacement
                //half4 waterFX = SAMPLE_TEXTURE2D_LOD(_WaterFXMap, sampler_ScreenTextures_linear_clamp, screenUV.xy, 0);
                //input.posWS.y += waterFX.w * 2 - 1;

                // After waves
                input.clipPos = TransformWorldToHClip(input.posWS);
                input.shadowCoord = ComputeScreenPos(input.clipPos);
                input.viewDir = SafeNormalize(_WorldSpaceCameraPos - input.posWS);

                // Fog
                input.fogFactorNoise.x = ComputeFogFactor(input.clipPos.z);
                input.preWaveSP = screenUV.xyz; // pre-displaced screenUVs

                // Additional data
                input.additionalData = AdditionalData(input.posWS, wave);

                // distance blend
                half distanceBlend = saturate(abs(length((_WorldSpaceCameraPos.xz - input.posWS.xz) * 0.005)) - 0.25);
                input.normal = lerp(input.normal, half3(0, 1, 0), distanceBlend);

                return input;
            }

            ///////////////////////////////////////////////////////////////////////////////
            //               	   Vertex and Fragment functions                         //
            ///////////////////////////////////////////////////////////////////////////////

            // Vertex: Used for Standard non-tessellated water
            WaterVertexOutput WaterVertex(WaterVertexInput v)
            {
                WaterVertexOutput o = (WaterVertexOutput)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.uv.xy = v.texcoord; // geo uvs
                o.posWS = TransformObjectToWorld(v.vertex.xyz);

                o = WaveVertexOperations(o);
                return o;
            }

            float4 WaterFragment(WaterVertexOutput IN) : SV_Target
            {
                // sample the texture
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                //discard;
                // apply fog
                return col;
            }
            ENDHLSL
        }
    }
}