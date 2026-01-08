#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl" //包含real类型的定义
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl" //包含感知粗糙度的转换函数
#include "UnityInput.hlsl" //unity的输入，包含一些矩阵等数据

//SpaceTransforms里使用宏定义进行运行，这里给宏定义相应的值
#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM 
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM 
#define UNITY_MATRIX_P glstate_matrix_projection

#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
    //定义宏，使遮挡探针能被实例化处理
    #define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl" //支持实例化
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl" //包含空间变换等函数


float Square(float v)
{
    return v*v;
}

float DistanceSquared(float3 pA, float3 pB)
{
    return dot(pA - pB, pA - pB);
}

void ClipLOD(float2 positionCS, float fade)
{
    #if defined(LOD_FADE_CROSSFADE)
        float dither = InterleavedGradientNoise(positionCS.xy,0);
        //淡出物体的fade为正数，淡入物体的fade为负数
        clip(fade + (fade < 0.0 ? dither : -dither));
    #endif
    
}

#endif
