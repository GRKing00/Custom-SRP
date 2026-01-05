using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    const string bufferName = "Shadows";
    
    CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName
    };

    ScriptableRenderContext context;
    
    CullingResults cullingResults;
    
    ShadowSettings settings;

    //最大带阴影的方向光数量,级联数量
    private const int maxShadowedDirectionalLightCount = 4, maxCascades = 4;

    //带阴影的方向光类
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;//可见光索引
        public float slopeScaleBias;//斜率偏移
        public float nearPlaneOffset;//光方向近平面偏移，防止渲染阴影时物体形变过大带来的阴影变形
    }
    
    //带阴影的方向光数组，用于渲染阴影
    ShadowedDirectionalLight[] ShadowedDirectionalLights = 
            new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
    
    //带阴影的方向光计数
    int ShadowedDirectionalLightCount;
    
    //过滤模式关键字
    private static string[] directionalFilterKeywords =
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };
    
    //级联混合关键字
    private static string[] cascadeBlendKeywords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    //阴影遮罩关键字
    private static string[] shadowMaskKeywords =
    {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE"
    };
    
    bool useShadowMask;
    
    //方向阴影图集和矩阵Id
    private static int
        dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
    
    //要上传到GPU的级联剔除球体数据和级联数据
    static Vector4[] 
        cascadeCullingSpheres = new Vector4[maxCascades],
        cascadeData = new Vector4[maxCascades];
    
    //方向阴影矩阵数据
    static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];
    
    
    public void Setup(
        ScriptableRenderContext context, CullingResults cullingResults,
        ShadowSettings settings
        )
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;
        ShadowedDirectionalLightCount = 0;
        useShadowMask = false;
    }
    
    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    //预存方向光阴影数据
    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        //不超过数量限制，渲染阴影且阴影强度大于0，阴影投射包围盒有效
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f
           )
        {
            float maskChannel = -1;
            //判断是否使用shadow Mask
            LightBakingOutput lightBaking = light.bakingOutput;
            if (
                lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
            )
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;//shadow mask使用的通道
            }
            //包围盒内没有阴影投射物
            if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            {
                return new Vector4(-light.shadowStrength, 0f, 0f,maskChannel);
            }
            ShadowedDirectionalLights[ShadowedDirectionalLightCount] =
                new ShadowedDirectionalLight { 
                    visibleLightIndex = visibleLightIndex, //第几个方向光会带有阴影
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane
                };
            return new Vector4(
                //返回阴影强度和带阴影方向光索引，这些数据会上传到GPU
                light.shadowStrength,settings.directional.cascadeCount * ShadowedDirectionalLightCount++,
                light.shadowNormalBias, maskChannel
            );
        }
        return new Vector4(0f,0f,0f,-1f);
    }

    //渲染阴影
    public void Render()
    {
        if (ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            //不渲染阴影时只申请一张1x1的阴影图集纹理
            buffer.GetTemporaryRT(
                dirShadowAtlasId,1,1,
                32,FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        }
        buffer.BeginSample(bufferName);
        SetKeywords(shadowMaskKeywords,useShadowMask?
            QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask? 0 : 1 :
            -1);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    //渲染方向光阴影
    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        buffer.GetTemporaryRT(
            dirShadowAtlasId, atlasSize,atlasSize,
            32,FilterMode.Bilinear,RenderTextureFormat.Shadowmap);//申请图集纹理
        buffer.SetRenderTarget(
            dirShadowAtlasId,
            RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store);//设置渲染目标
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;//划分次数
        int tileSize = atlasSize / split;//每块大小
        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }
        buffer.SetGlobalInt(cascadeCountId,settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        //上传方向阴影矩阵，将世界空间位置转阴影图集纹理空间
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        float f = 1f - settings.directional.cascadeFade;//级联渐隐
        buffer.SetGlobalVector(
            shadowDistanceFadeId,
            new Vector4(1f/settings.maxDistance, 1f/settings.distanceFade, 1f / (1f - f * f)));
        //阴影采样过滤关键字和级联混合关键字
        SetKeywords(directionalFilterKeywords,(int)settings.directional.filter -1);
        SetKeywords(cascadeBlendKeywords,(int)settings.directional.cascadeBlend -1);
        buffer.SetGlobalVector(
            shadowAtlasSizeId,new Vector4(atlasSize,1f/atlasSize)
        );
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    //设置阴影过滤器关键字
    void SetKeywords(string[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        //是否使用反向Z缓冲
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        //求取世界空间到阴影图集纹理空间的矩阵
        float scale = 1f/split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }

    //设置每一块的视口
    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);//xy的偏移
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    //将方向光阴影渲染到图集上
    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        //阴影渲染设置
        var shadowSettings = 
            new ShadowDrawingSettings(cullingResults,light.visibleLightIndex,BatchCullingProjectionType.Orthographic);
        int cascadeCount = settings.directional.cascadeCount;
        //光源的阴影偏移多少块
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;
        //计算级联剔除因子
        float cullingFactor = Mathf.Max(0f,0.8f - settings.directional.cascadeFade);
        for (int i = 0; i < cascadeCount; i++)
        {
            //生成裁剪空间立方体
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, 
                light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowSettings.splitData = splitData;
            if (index == 0)
            {
                SetCascadeData(i,splitData.cullingSphere,tileSize);
            }
            //当前级联所在块
            int tileIndex = tileOffset + i;
            //设置视口，并求取世界空间到阴影图集纹理空间的矩阵
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix( 
                projectionMatrix * viewMatrix,SetTileViewport(tileIndex, split, tileSize),split
            );
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);//设置光空间VP矩阵
            buffer.SetGlobalDepthBias(0f,light.slopeScaleBias);//添加斜率偏移
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);//渲染阴影，只会渲染带有ShadowCaster Pass的物体
            buffer.SetGlobalDepthBias(0f,0f);//撤销偏移
        }

    }

    void SetCascadeData(int index, Vector4 cullingSphere, int tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;//纹素大小
        //考虑PCF过滤，放大朝法线方向偏移的量
        float filterSize = texelSize * ((float)settings.directional.filter + 1f);
        //防止采样超出图集范围
        cullingSphere.w -= filterSize;
        //所有方向光的剔除球体是一样的，只获取第一个方向光的剔除球体数据
        cullingSphere.w *= cullingSphere.w;//上传半径的平方
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(
            1f / cullingSphere.w,
            filterSize * 1.4142136f
        );
    }
    
    //释放阴影图集纹理
    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }
    
}
