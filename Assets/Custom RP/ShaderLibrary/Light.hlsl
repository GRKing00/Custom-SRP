#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

//定义灯光结构体，获取灯光数据


#define MAX_DIRECTIONAL_LIGHT_COUNT 4

//CPU上传的灯光数据
CBUFFER_START(_CustomLight)
    int _DirectionalLightCount;
    float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

struct Light
{
    float3 color;
    float3 direction;
    float attenuation;
};

int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

//获取方向光阴影数据
DirectionalShadowData GetDirectionalShadowData(int lightIndex, ShadowData shadowData)
{
    DirectionalShadowData data;
    data.strength = _DirectionalLightShadowData[lightIndex].x;
    data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;
    data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;//对应的通道
    return data;
}

//获取方向光数据
Light GetDirectionalLight(int index, Surface surfaceWS, ShadowData shadowData)
{
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirections[index].xyz;
    DirectionalShadowData dirShadowData = GetDirectionalShadowData(index,shadowData);
    //获取衰减，里会使用Shadow Matrices计算采样阴影图集的uv
    light.attenuation = GetDirectionalShadowAttenuation(dirShadowData,shadowData,surfaceWS);
    
    return light;
}


#endif
