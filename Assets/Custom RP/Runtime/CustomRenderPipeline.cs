using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

//管线实例类
public partial class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer = new CameraRenderer();
    
    bool useDynamicBatching, useGPUInstancing, useLightsPerObject;

    ShadowSettings shadowSettings;
    
    public CustomRenderPipeline(
        bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,bool useLightsPerObject, 
        ShadowSettings shadowSettings
        )
    {
        this.shadowSettings = shadowSettings;
        //批处理相关设置
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;
        InitializeForEditor();
    }
    
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        
    }
    
    //unity会自动调用管线的Render函数，我们只需要在Render中定义自己的渲染操作
    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        //每个相机单独渲染
        for (int i = 0; i < cameras.Count; ++i)
        {
            renderer.Render(context, cameras[i],useDynamicBatching,useGPUInstancing,useLightsPerObject,shadowSettings);
        }
    }

}
