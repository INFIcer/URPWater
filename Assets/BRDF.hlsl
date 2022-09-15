#ifndef XXX
#define XXX
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
void BRDF_half(half3 Albedo, half Metallic, half3 Specular, half Smoothness, half Alpha,half3 Normal,half3 LightDirection,half3 ViewDir,out half3 Color)
{
	BRDFData brdfData;
	InitializeBRDFData(Albedo, Metallic, Specular,Smoothness, Alpha, brdfData);
	Color = DirectBDRF(brdfData, Normal, LightDirection, ViewDir);
}
#endif