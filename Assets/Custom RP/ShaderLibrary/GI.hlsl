#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

//包含间接光照相关的内容

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

//光照贴图
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

//探针体积纹理
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

//使用光照贴图时定义宏数据
#if defined(LIGHTMAP_ON)
    //光照贴图的uv在第二套uv中，即TEXCOORD1
    #define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;
    #define GI_VARYINGS_DATA float2 lightMapUV : VAR_LIGHT_MAP_UV;
    #define TRANSFER_GI_DATA(input, output) \
        output.lightMapUV = input.lightMapUV * \
        unity_LightmapST.xy + unity_LightmapST.zw;
    #define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
    #define GI_ATTRIBUTE_DATA
    #define GI_VARYINGS_DATA
    #define TRANSFER_GI_DATA(input, output)
    #define GI_FRAGMENT_DATA(input) 0.0
#endif



struct GI
{
    float3 diffuse;
};

//采样光照贴图
float3 SampleLightMap(float2 lightMapUV)
{
    #if defined(LIGHTMAP_ON)
        return SampleSingleLightmap(
                TEXTURE2D_ARGS(unity_Lightmap,samplerunity_Lightmap),lightMapUV,
                float4(1.0,1.0,0.0,0.0),//此处的lightMapUV已经应用缩放和偏移，所以使用1,1,0,0
                #if defined(UNITY_LIGHTMAP_FULL_HDR)
                    false,
                #else
                    true,
                #endif
                    float4(LIGHTMAP_HDR_MULTIPLIER,LIGHTMAP_HDR_EXPONENT,0.0,0.0) //解码指令
            );
    #else 
        return 0.0;
    #endif
    
}

//采样光照探针
float3 SampleLightProbe(Surface surfaceWS)
{
    #if defined(LIGHTMAP_ON)
        //如果采样光照贴图则不采样光照探针
        return 0.0;
    #else
        //使用探针体积
        if (unity_ProbeVolumeParams.x)
        {
            return SampleProbeVolumeSH4(
                TEXTURE3D_ARGS(unity_ProbeVolumeSH,samplerunity_ProbeVolumeSH),
                surfaceWS.position,surfaceWS.normal,
                unity_ProbeVolumeWorldToObject,
                unity_ProbeVolumeParams.y,unity_ProbeVolumeParams.z,
                unity_ProbeVolumeMin.xyz,unity_ProbeVolumeSizeInv.xyz
            ); 
        }
        else
        {
            float4 coefficients[7];
            coefficients[0] = unity_SHAr;
            coefficients[1] = unity_SHAg;
            coefficients[2] = unity_SHAb;
            coefficients[3] = unity_SHBr;
            coefficients[4] = unity_SHBg;
            coefficients[5] = unity_SHBb;
            coefficients[6] = unity_SHC;
            return max(0.0, SampleSH9(coefficients,surfaceWS.normal));   
        }

   # endif 
}


GI GetGI(float2 lightMapUV, Surface surfaceWS)
{
    GI gi;
    gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
    return gi;
}


#endif
