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
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    
    //渲染操作
    public void Render(ScriptableRenderContext context, Camera camera)
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull())
        {
            return;
        }
        
        Setup();
        DrawVisibleGeometry();
        DrawUnsupportedShaders();
        DrawGizmos();
        Submit();
        
    }
    
    //剔除操作，获取剔除结果
    bool Cull()
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
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
    void DrawVisibleGeometry()
    {
        //不透明物体
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque //不透明物体从前往后的顺序渲染
        };
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings);//渲染设置，可执行的着色器，和排序设置
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
