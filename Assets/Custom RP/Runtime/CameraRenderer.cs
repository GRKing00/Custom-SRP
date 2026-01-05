using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

//相机渲染器，用于渲染单个相机看到的内容
public partial class CameraRenderer
{
    ScriptableRenderContext context;
    
    Camera camera;

    //命令缓冲
    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName
    };
    
    //剔除结果
    CullingResults cullingResults;
    
    //可执行着色器的标签
    static ShaderTagId 
        unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");
    
    Lighting lighting = new Lighting();
    
    //渲染操作
    public void Render(ScriptableRenderContext context, Camera camera,
        bool useDynamicBatching, bool useGPUInstancing, ShadowSettings shadowSettings)
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        lighting.Setup(context,cullingResults,shadowSettings);//灯光设置和渲染阴影
        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching,useGPUInstancing);
        DrawUnsupportedShaders();
        DrawGizmos();
        lighting.Cleanup();//清除阴影图集纹理
        Submit();
        
    }
    
    //剔除操作，获取剔除结果
    bool Cull(float maxShadowDistance)
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            //阴影距离为最大阴影距离和远裁剪平面的最小值
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        
        return false;
    }
    
    //一些渲染相关配置
    void Setup()
    {
        context.SetupCameraProperties(camera);//配置相机属性,包含VP矩阵等
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth, 
            flags <=CameraClearFlags.Color, 
            flags == CameraClearFlags.Color?
                camera.backgroundColor.linear : Color.clear);//清空渲染目标的缓冲
        buffer.BeginSample(SampleName);//注入性能分析采样
        ExecuteBuffer();
    }
    
    //绘制可见几何
    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
    {
        //不透明物体
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque //不透明物体从前往后的顺序渲染
        };
        //渲染设置，可执行的着色器，和排序设置
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            //是否开启动态批处理和实例化
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.Lightmaps | PerObjectData.ShadowMask | 
                            PerObjectData.LightProbe| //传递光照贴图数据和光照探针数据
                            PerObjectData.OcclusionProbe | //遮挡探针
                            PerObjectData.LightProbeProxyVolume | //传递光照探针代理数据
                            PerObjectData.OcclusionProbeProxyVolume //遮挡探针代理体积
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);//过滤设置，只渲染不透明物体
        
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);//渲染不透明物体
        
        context.DrawSkybox(camera);
        
        //透明物体
        sortingSettings.criteria = SortingCriteria.CommonTransparent;//透明物体从后往前的顺序渲染
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;//过滤设置，只渲染透明物体
        
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);//渲染透明物体
        
    }

    //提交后渲染操作才会执行
    void Submit()
    {
        buffer.EndSample(SampleName);//结束性能分析采样
        ExecuteBuffer();
        context.Submit();
    }

    //上下文执行缓冲命令
    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

}
