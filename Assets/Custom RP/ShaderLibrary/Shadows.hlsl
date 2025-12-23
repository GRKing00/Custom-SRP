#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED


//包含从CPU上传的阴影数据

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined (_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined (_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHTS_COUNT 4
#define MAX_CASCADE_COUNT 4

//阴影图集
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

//CPU端上传的阴影数据
CBUFFER_START(_CustomShadows)
    int _CascadeCount;
    float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    float4 _CascadeData[MAX_CASCADE_COUNT];
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHTS_COUNT * MAX_CASCADE_COUNT]; //世界空间到阴影图集纹理空间的矩阵
    float4 _ShadowAtlasSize;
    float4 _ShadowDistanceFade;
CBUFFER_END

struct DirectionalShadowData
{
    float strength;
    int tileIndex;
    float normalBias;
};

//获取阴影数据
struct ShadowData
{
    int cascadeIndex;
    float cascadeBlend;//级联之间的混合过度
    float strength;
};

//最大距离渐隐
float FadeShadowStrength(float distance,float scale, float fade)
{
    return saturate((1.0-distance * scale)*fade);
}

//获取阴影数据
ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData data;
    data.cascadeBlend = 1.0;
    data.strength = FadeShadowStrength(surfaceWS.depth,_ShadowDistanceFade.x,_ShadowDistanceFade.y);
    int i;
    for (i=0;i<_CascadeCount;i++)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float  distanceSqr = DistanceSquared(surfaceWS.position,sphere.xyz);
        //判断当前位置是否在级联球体内
        if (distanceSqr < sphere.w)
        {
            float fade = FadeShadowStrength(
                    distanceSqr,_CascadeData[i].x,_ShadowDistanceFade.z
                ); 
            if (i == _CascadeCount - 1)
            {
                //级联渐隐
                data.strength *= fade;
            }
            else
            {
                data.cascadeBlend = fade;//级联过度
            }
            break;
        }
    }
    //级联之外不渲染阴影
    if (i==_CascadeCount)
    {
        data.strength = 0.0;
    }
    //如果使用级联扰动，则当级联混合值小于抖动值时选择下一层的级联
    #if defined(_CASCADE_BLEND_DITHER)
        else if (data.cascadeBlend < surfaceWS.dither)
        {
            i+=1;
        }
    #endif
    //没有定义级联软混合，则取消级联混合
    #if !defined(_CASCADE_BLEND_SOFT)
        data.cascadeBlend = 1.0;
    #endif
    data.cascadeIndex =i;
    
    return data;
}

//采样方向阴影图集
float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(
        _DirectionalShadowAtlas,SHADOW_SAMPLER,positionSTS   
    );
}

float FilterDirectionalShadow(float3 positionSTS)
{
    #if defined(DIRECTIONAL_FILTER_SETUP)
        //PCF过滤
        float weights[DIRECTIONAL_FILTER_SAMPLES];
        float2 positions[DIRECTIONAL_FILTER_SAMPLES];
        float4 size = _ShadowAtlasSize.yyxx;
        DIRECTIONAL_FILTER_SETUP(size,positionSTS.xy, weights, positions);
        float shadow =0;
        for (int i=0;i<DIRECTIONAL_FILTER_SAMPLES;i++)
        {
            shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy,positionSTS.z));
        }
        return shadow;
    #else
        return SampleDirectionalShadowAtlas(positionSTS);
    #endif
}

//获取阴影衰减
float GetDirectionalShadowAttenuation(DirectionalShadowData directional,ShadowData global, Surface surfaceWS)
{
    //如果不接受阴影，则不衰减
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #endif
    
    //阴影强度为0时没有阴影
    if (directional.strength <= 0.0f)
    {
        return 1.0f;
    }
    //朝法线方向偏移的强度
    float3 normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
    //纹理空间坐标
    float3 positionSTS = mul(
        _DirectionalShadowMatrices[directional.tileIndex],
        float4(surfaceWS.position + normalBias,1.0)  //朝法线方向进行偏移，然后再计算纹理空间坐标
    ).xyz;
    //从阴影图集中采样阴影
    float shadow = FilterDirectionalShadow(positionSTS);
    if (global.cascadeBlend <1.0)
    {
        //处于级联阴影过度区域，计算下一级的级联，插值两层级联
        normalBias = surfaceWS.normal *
            (directional.normalBias * _CascadeData[global.cascadeIndex+1].y);
        positionSTS = mul(
            _DirectionalShadowMatrices[directional.tileIndex+1],
            float4(surfaceWS.position + normalBias,1.0)
        ).xyz;
        shadow = lerp(FilterDirectionalShadow(positionSTS),shadow,global.cascadeBlend);
    }
    return lerp(1.0, shadow, directional.strength);
}

#endif
