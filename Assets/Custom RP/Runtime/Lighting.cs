using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    const string bufferName = "Lighting";
    
    CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName
    };

    CullingResults cullingResults;
    
    const int maxDirLightCount = 4;
    
    //着色器属性ID
    private static int
        dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
    
    //需要上传至GPU的数据
    static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];
    
    //阴影实例，包含一些阴影的配置
    Shadows shadows = new Shadows();
    
    //设置灯光数据
    public void Setup(ScriptableRenderContext contex,CullingResults cullingResults ,ShadowSettings shadowSettings)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        shadows.Setup(contex,cullingResults,shadowSettings);//配置阴影
        SetupLights();//这里会预定阴影
        shadows.Render();//渲染阴影
        buffer.EndSample(bufferName);
        contex.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    //上传所有光照数据
    void SetupLights()
    {
        int dirLightCount = 0;//统计方向光数量
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;//获取可见光数据
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            if (visibleLight.lightType == LightType.Directional)
            {
                SetupDirectionalLight(dirLightCount++, ref visibleLight);
                if (dirLightCount >= maxDirLightCount)//限制数量
                {
                    break;
                }
            }

        }
        
        buffer.SetGlobalInt(dirLightCountId,dirLightCount);
        buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
        buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
    }

    //设置方向光数据
    void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index);//预定方向光阴影
    }

    public void Cleanup()
    {
        shadows.Cleanup();
    }
}
    
