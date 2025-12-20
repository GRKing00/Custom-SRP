using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

//管线资产类
[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    //创建渲染管线实例
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline();
    }
}
