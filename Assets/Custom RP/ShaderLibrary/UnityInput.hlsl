#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

//Unity中内置的输入，这些数据都是从CPU端上传至GPU端，供Shader里使用

//每物体常量
CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    real4 unity_WorldTransformParams;

    real4 unity_LightData;
    real4 unity_LightIndices[2];

    float4 unity_ProbesOcclusion;
    float4 unity_SpecCube0_HDR;
    //CPU端上传的光照贴图uv的缩放和偏移
    float4 unity_LightmapST;
    float4 unity_DynamicLightmapST;

    //CPU端上传的光照探针数据
    float4 unity_SHAr;
    float4 unity_SHAg;
    float4 unity_SHAb;
    float4 unity_SHBr;
    float4 unity_SHBg;
    float4 unity_SHBb;
    float4 unity_SHC;

    //CPU端上传的光照探针代理数据
    float4 unity_ProbeVolumeParams;
    float4x4 unity_ProbeVolumeWorldToObject;
    float4 unity_ProbeVolumeSizeInv;
    float4 unity_ProbeVolumeMin;
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;
float4x4 glstate_matrix_projection;

float3 _WorldSpaceCameraPos;
bool4 unity_MetaFragmentControl;
float unity_OneOverOutputBoost;
float unity_MaxOutputValue;

#endif