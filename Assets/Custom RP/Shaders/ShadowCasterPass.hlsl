#ifndef CUSTOM_SHADOW_CASTER_PASS_INCLUDED
#define CUSTOM_SHADOW_CASTER_PASS_INCLUDED

struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID //顶点实例ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

bool _ShadowPancaking;

Varyings ShadowCasterPassVertex(Attributes input) 
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);//设置实例ID，之后根据实例ID获取材质数据
    UNITY_TRANSFER_INSTANCE_ID(input,output);//传递实例ID
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
    
    if (_ShadowPancaking)
    {
        //将近裁剪平面之前的顶点压到近裁剪平面
        #if UNITY_REVERSED_Z
        output.positionCS.z = 
            min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
        #else 
        output.positionCS.z =
            max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
        #endif
    }


    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}

void ShadowCasterPassFragment(Varyings input) 
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipLOD(input.positionCS.xy, unity_LODFade.x);
    InputConfig config = GetInputConfig(input.baseUV);
    float4 base = GetBase(config);
    //使用阴影裁剪模式
    #if defined(_SHADOWS_CLIP)
        clip(base.a - GetCutoff(config));
    #elif defined(_SHADOWS_DITHER)
        //使用阴影抖动
        float dither = InterleavedGradientNoise(input.positionCS.xy,0);
        clip(base.a - dither);
    #endif
}

#endif

