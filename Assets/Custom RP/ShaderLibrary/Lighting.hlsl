#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED
//光照计算

//输入光照，NdotL * 灯光颜色
float3 IncomingLight(Surface surface, Light light)
{
    return saturate(dot(surface.normal, light.direction) * light.attenuation) * light.color;
}

//单个灯光的光照结果，输入光照 * 反射率
float3 GetLighting(Surface surface, BRDF brdf, Light light)
{
    return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

//计算所有光照的结果
float3 GetLighting(Surface surfaceWS, BRDF brdf)
{
    ShadowData shadowData = GetShadowData(surfaceWS);
    float3 color = 0.0;
    for (int i=0;i<GetDirectionalLightCount();i++)
    {
        Light light = GetDirectionalLight(i,surfaceWS,shadowData);
        color +=GetLighting(surfaceWS, brdf, light);
    }
    return color;
}

#endif