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
    int shadowMaskChannel;
};

struct OtherShadowData
{
    float strength;
    int shadowMaskChannel;
};

struct ShadowMask
{
    bool always;
    bool distance;
    float4 shadows;
};

//获取阴影数据
struct ShadowData
{
    int cascadeIndex;//第几个级联
    float cascadeBlend;//级联之间的混合过度
    float strength;//级联阴影强度，用于渐隐最外层阴影
    ShadowMask shadowMask;
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
    data.shadowMask.always = false;
    data.shadowMask.distance = false;
    data.shadowMask.shadows = 1.0;
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

//采样级联阴影
float GetCascadedShadow(
    DirectionalShadowData directional, ShadowData global, Surface surfaceWS    
)
{
    //朝法线方向偏移的强度
    float3 normalBias = surfaceWS.interpolatedNormal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
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
        normalBias = surfaceWS.interpolatedNormal *
            (directional.normalBias * _CascadeData[global.cascadeIndex+1].y);
        positionSTS = mul(
            _DirectionalShadowMatrices[directional.tileIndex+1],
            float4(surfaceWS.position + normalBias,1.0)
        ).xyz;
        shadow = lerp(FilterDirectionalShadow(positionSTS),shadow,global.cascadeBlend);
    }
    return shadow;
}
//获取烘焙阴影的数据
float GetBakedShadow(ShadowMask mask, int channel)
{
    float shadow = 1.0;
    if (mask.always || mask.distance)
    {
        if (channel >= 0)
        {
            shadow = mask.shadows[channel];
        }
    }
    return shadow;
}

//经过强度插值的阴影
float GetBakedShadow(ShadowMask mask, int channel, float strength)
{
    if (mask.always || mask.distance)
    {
        return lerp(1.0, GetBakedShadow(mask, channel), strength);
    }
    return 1.0;
}

//混烘焙阴影和实时阴影
float MixBakedAndRealtimeShadows(
    ShadowData global, float shadow, int shadowMaskChannel, float strength    
)
{
    float baked = GetBakedShadow(global.shadowMask, shadowMaskChannel);
    //总是使用烘焙阴影
    if (global.shadowMask.always)
    {
        //实时阴影
        shadow = lerp(1.0, shadow, global.strength);
        //实时阴影与烘焙阴影取更小的那个，在shadowmask模式下，会省略静态物体的阴影投射，取min即可混合静态烘焙阴影与动态物体的实时阴影
        shadow = min(baked, shadow);
        return lerp(1.0, shadow, strength);
    }
    //阴影距离外使用烘焙阴影
    if (global.shadowMask.distance)
    {
        //实时阴影和烘焙阴影插值
        shadow = lerp(baked, shadow,global.strength);
        //基于光源的阴影强度插值
        return lerp(1.0,shadow,strength);
    }
    return lerp(1.0, shadow, strength * global.strength);
}

//获取阴影衰减
float GetDirectionalShadowAttenuation(DirectionalShadowData directional,ShadowData global, Surface surfaceWS)
{
    //如果不接受阴影，则不衰减
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #endif
    
    float shadow;
    //阴影强度小于0时,考虑使用烘焙阴影
    if (directional.strength * global.strength <= 0.0f)
    {
        shadow = GetBakedShadow(global.shadowMask, directional.shadowMaskChannel, abs(directional.strength));
    }
    else
    {
        //采样阴影贴图并混合烘焙阴影
        shadow = GetCascadedShadow(directional,global,surfaceWS);
        shadow = MixBakedAndRealtimeShadows(global,shadow,directional.shadowMaskChannel,directional.strength);
    }

    return shadow;
}

//获取其他光的阴影衰减
float GetOtherShadowAttenuation(OtherShadowData other, ShadowData global, Surface surfaceWS)
{
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #endif
    
    float shadow;
    if (other.strength  > 0.0)
    {
        //现在仅返回烘焙阴影
        shadow = GetBakedShadow(global.shadowMask, other.shadowMaskChannel, other.strength);
    }
    else
    {
        shadow = 1.0;
    }
    return shadow;
    
}

#endif
