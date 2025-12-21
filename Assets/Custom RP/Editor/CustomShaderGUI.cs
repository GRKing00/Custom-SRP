using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

//自定义shaderGUI
public class CustomShaderGUI : ShaderGUI
{
    MaterialEditor editor;
    Object[] materials; //正在被编辑的材质,可以一次选择多个材质
    MaterialProperty[] properties;
    
    
    //定义属性设置
    bool Clipping
    {
        set=>SetProperty("_Clipping","_CLIPPING", value);
    }

    bool PremultiplyAlpha
    {
        set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
    }

    BlendMode SrcBlend
    {
        set => SetProperty("_SrcBlend", (float)value);
    }
    
    BlendMode DstBlend
    {
        set => SetProperty("_DstBlend", (float)value);
    }

    bool ZWrite
    {
        set => SetProperty("_ZWrite", value? 1f:0f);
    }

    //设置材质的渲染队列
    RenderQueue RenderQueue
    {
        set
        {
            foreach (Material m in materials)
            {
                m.renderQueue = (int)value;
            }
        }
    }

    bool showPresets;//预设开关折叠和展开
    
    public override void OnGUI(
        MaterialEditor materialEditor, MaterialProperty[] properties
        )
    {
        base.OnGUI(materialEditor, properties);
        editor = materialEditor;
        materials = materialEditor.targets;
        this.properties = properties;
        
        //折叠或展开预设
        EditorGUILayout.Space();
        showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
        if (showPresets)
        {
            OpaquePreSet();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }

    }
    
    bool HasProperty(string name) =>
        FindProperty(name, properties,false) != null;

    bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");//是否有预乘属性
    
    //设置属性
    bool SetProperty(string name, float value)
    {
        //在属性数组中查找指定属性，找不到返回null
        MaterialProperty property = FindProperty(name, properties,false);
        if (property != null)
        {
            property.floatValue = value;
            return true;
        }
        return false;
    }

    //设置关键字
    void SetKeyword(string keyword, bool enabled)
    {
        if (enabled)
        {
            //所有材质启用关键字
            foreach (Material m in materials)
            {
                m.EnableKeyword(keyword);   
            }
        }
        else
        {
            //所有材质禁用关键字
            foreach (Material m in materials) 
            {
                m.DisableKeyword(keyword);
            }
        }
    }

    //设置属性开关和关键字
    void SetProperty(string name, string keyword, bool value)
    {
        if (SetProperty(name, value ? 1f : 0f))
        {
            SetKeyword(keyword, value);
        }
    }

    //预设按钮
    bool PresetButton(string name)
    {
        if (GUILayout.Button(name)) //创建按钮,返回true则按钮被按下
        {
            editor.RegisterPropertyChangeUndo(name);//注册撤销步骤,可以Ctrl+z撤销操作
            return true;
        }
        return false;
    }

    //预设
    void OpaquePreSet()
    {
        if (PresetButton("Opaque"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
        }
    }

    void ClipPreset()
    {
        if (PresetButton("Clip"))
        {
            Clipping = true;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
        }

    }
    
    void FadePreset()
    {
        if (PresetButton("Fade"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }

    }
    //预乘
    void TransparentPreset()
    {
        if (HasPremultiplyAlpha && PresetButton("Transparent"))
        {
            Clipping = false;
            PremultiplyAlpha = true;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }

    }
    

}
