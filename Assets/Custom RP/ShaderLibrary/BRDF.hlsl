#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

//定义BRDF相关属性和BRDF的计算函数

struct BRDF
{
    float3 diffuse; //漫反射率
    float3 specular; //镜面反射率
    float roughness; //粗糙度
};

#define MIN_REFLECTIVITY 0.04

//得到 1 - 反射率
float OneMinusReflectivity(float metallic)
{
    float range = 1.0 - MIN_REFLECTIVITY;
    return range - metallic * range;
}

//计算高光强度
float SpecularStrength(Surface surface, BRDF brdf, Light light)
{
    float3 h = SafeNormalize(light.direction + surface.viewDirection);
    float nh2 = Square(saturate(dot(surface.normal, h)));
    float lh2 = Square(saturate(dot(light.direction, h)));
    float r2 = Square(brdf.roughness);
    float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
    float normalization = brdf.roughness * 4.0 + 2.0;
    return r2 / (d2 * max(0.1,lh2) * normalization);
}

//方向光BRDF的结果
float3 DirectBRDF(Surface surface, BRDF brdf, Light light)
{
    return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

//获取BRDF相关数据
BRDF GetBRDF(Surface surface, bool applyAlphaToDiffuse = false)
{
    BRDF brdf;
    float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
    brdf.diffuse = surface.color * oneMinusReflectivity;
    if (applyAlphaToDiffuse)
    {
        brdf.diffuse *= surface.alpha; //预乘alpha, 只会减弱漫反射，镜面反射强度不变
    }
    brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);
    float perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
    brdf.roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
    return brdf;
}

#endif
