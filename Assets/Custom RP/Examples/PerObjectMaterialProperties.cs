using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//每对象材质属性，每个对象都有自己的材质属性
[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    //shader属性ID
    private static int
        baseColorId = Shader.PropertyToID("_BaseColor"),
        cutoffId = Shader.PropertyToID("_Cutoff"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness");
    
    
    [SerializeField]
    Color baseColor = Color.white;
    
    [SerializeField, Range(0f, 1f)]
    float cutoff = 0.5f, metallic = 0f, smoothness = 0.5f;
    
    static MaterialPropertyBlock block;

    //属性修改时调用
    private void OnValidate()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
        //设置块属性
        block.SetColor(baseColorId, baseColor);
        block.SetFloat(cutoffId, cutoff);
        block.SetFloat(metallicId,metallic);
        block.SetFloat(smoothnessId, smoothness);
        GetComponent<Renderer>().SetPropertyBlock(block);//给物体的渲染器设置当前的材质属性块
    }

    //唤醒时调用一次
    void Awake()
    {
        OnValidate();
    }
}
