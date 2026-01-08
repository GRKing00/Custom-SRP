#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

//包含间接光照相关的内容
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

//光照贴图
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

//探针体积纹理
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

//ShadowMask 纹理
TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);

//环境纹理
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

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
    float3 specular;
    ShadowMask shadowMask;
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

//采样烘焙阴影
float4 SampleBakedShadows(float2 lightMapUV, Surface surfaceWS)
{
    #if defined(LIGHTMAP_ON)
        return SAMPLE_TEXTURE2D(
            unity_ShadowMask, samplerunity_ShadowMask,lightMapUV    
        );
    #else
        if (unity_ProbeVolumeParams.x)
        {
            //使用遮挡探针体积
            return SampleProbeOcclusion(
                TEXTURE3D_ARGS(unity_ProbeVolumeSH,samplerunity_ProbeVolumeSH),
                surfaceWS.position,unity_ProbeVolumeWorldToObject,
                unity_ProbeVolumeParams.y,unity_ProbeVolumeParams.z,
                unity_ProbeVolumeMin.xyz,unity_ProbeVolumeSizeInv.xyz
            ); 
        }
        else
        {
            //返回遮挡探针中的数据
            return unity_ProbesOcclusion;
        }

    #endif
}

//采样环境纹理
float3 SampleEnvironment(Surface surfaceWS, BRDF brdf)
{
    float3 uvw =reflect(-surfaceWS.viewDirection, surfaceWS.normal);
    float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
    float4 environment = SAMPLE_TEXTURECUBE_LOD(
        unity_SpecCube0,samplerunity_SpecCube0,uvw,mip    
    );
    return DecodeHDREnvironment(environment,unity_SpecCube0_HDR);
}

GI GetGI(float2 lightMapUV, Surface surfaceWS, BRDF brdf)
{
    GI gi;
    gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
    gi.specular = SampleEnvironment(surfaceWS,brdf);
    gi.shadowMask.always = false;
    gi.shadowMask.distance = false;
    gi.shadowMask.shadows = 1.0;
    #if defined(_SHADOW_MASK_ALWAYS)
        gi.shadowMask.always = true;
        gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
    #elif defined(_SHADOW_MASK_DISTANCE)
        gi.shadowMask.distance = true;
        gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
    #endif
    
    return gi;
}


#endif
