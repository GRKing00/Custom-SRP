using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

//相机渲染器，用于渲染单个相机看到的内容
partial class CameraRenderer
{
    partial void PrepareBuffer();
    partial void PrepareForSceneWindow();
    
    partial void DrawUnsupportedShaders();
    
    partial void DrawGizmos();



#if UNITY_EDITOR
    //不支持着色器的标签
    private static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };
    
    //错误材质
    static Material errorMaterial;

    //预处理命令缓冲名和性能分析采样名，在编辑器里与相机名相同
    string SampleName { get; set; }
    partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        buffer.name = SampleName = camera.name;//命令缓冲名 = 性能分析采样名 = 相机名
        Profiler.EndSample();
    }
    
    //预处理场景窗口
    partial void PrepareForSceneWindow()
    {
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);//确保能绘制UI
        }
    }


    //用默认的错误材质绘制不支持的着色器
    partial void DrawUnsupportedShaders()
    {
        if (errorMaterial == null)
        {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));//使用内置的错误材质
        }
        //使用默认设置
        var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(camera))
        {
            overrideMaterial = errorMaterial //使用内置错误材质覆盖
        };
        for (int i = 1; i < legacyShaderTagIds.Length; ++i) //其他不支持的着色器
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        var filteringSettings = FilteringSettings.defaultValue;
        
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }
    
    //绘制Gizmos
    partial void DrawGizmos()
    {
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }
    
#else

    const string SampleName = bufferName; //性能分析采样名 = 命令缓冲名 
    
#endif
    
}
