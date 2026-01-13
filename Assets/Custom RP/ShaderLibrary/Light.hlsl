#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

//定义灯光结构体，获取灯光数据


#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64 

//CPU上传的灯光数据
CBUFFER_START(_CustomLight)
    int _DirectionalLightCount;
    float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];

    int _OtherLightCount;
    float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightDirections[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
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

int GetOtherLightCount()
{
    return _OtherLightCount;
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

OtherShadowData GetOtherShadowData(int lightIndex)
{
    OtherShadowData data;
    data.strength = _OtherLightShadowData[lightIndex].x;
    data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
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

Light GetOtherLight(int index, Surface surfaceWS, ShadowData shadowData)
{
    Light light;
    light.color = _OtherLightColors[index].rgb;
    float3 ray = _OtherLightPositions[index].xyz - surfaceWS.position;
    light.direction = normalize(ray);
    //计算其他光的衰减
    //距离平方衰减
    float distanceSqr = max(dot(ray,ray),0.00001);
    //范围衰减
    float rangeAttenuation = Square(saturate(1.0 - Square(distanceSqr * _OtherLightPositions[index].w)));
    //聚光的内外角衰减
    float4 spotAngles = _OtherLightSpotAngles[index];
    float spotAttenuation = Square(saturate(dot(_OtherLightDirections[index].xyz,light.direction) * spotAngles.x + spotAngles.y));
    //阴影衰减
    OtherShadowData otherShadowData = GetOtherShadowData(index);
    light.attenuation = GetOtherShadowAttenuation(otherShadowData,shadowData,surfaceWS) *  
        spotAttenuation * rangeAttenuation / distanceSqr;
    return light;
}


#endif
