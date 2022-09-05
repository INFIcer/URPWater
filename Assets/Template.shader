Shader "Unlit/Template"
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
			Tags
			{
				"LightMode"="UniversalForward"
			}

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			//#pragma multi_compile_fog

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			CBUFFER_START(UnityPerMaterial)
			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			CBUFFER_END

			struct Attributes
			{
				float3 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 positionWS:TEXCOORD1;
			};


			Varyings vert(Attributes IN)
			{
				Varyings o;
				o.positionWS = TransformObjectToWorld(IN.positionOS);
				o.positionCS = TransformWorldToHClip(o.positionWS);
				o.uv = IN.uv;
				//UNITY_TRANSFER_FOG(o, o.vertex);
				return o;
			}

			float4 frag(Varyings IN) : SV_Target
			{
				// sample the texture
				float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
				// apply fog
				//UNITY_APPLY_FOG(IN.fogCoord, col);
				return col;
			}
			ENDHLSL
		}
	}
}