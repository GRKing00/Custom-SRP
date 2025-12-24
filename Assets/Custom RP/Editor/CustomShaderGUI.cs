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

    enum ShadowMode
    {
        On,Clip,Dither,Off
    }

    ShadowMode Shadows
    {
        set
        {
            
            if (SetProperty("_Shadows", (float)value))
            {
                //设置阴影模式的关键字
                SetKeyword("_SHADOWS_CLIP",value == ShadowMode.Clip);
                SetKeyword("_SHADOWS_DITHER",value == ShadowMode.Dither);
            }
        }
    }
    
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
        EditorGUI.BeginChangeCheck();
        base.OnGUI(materialEditor, properties);
        editor = materialEditor;
        materials = materialEditor.targets;
        this.properties = properties;
        
        BakedEmission();
        
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
        //如果检测到材质面板的改变，则尝试设置开启阴影投射pass
        if (EditorGUI.EndChangeCheck())
        {
            SetShadowCasterPass();
            CopyLightMappingProperties();
        }

    }

    //使半透明和裁剪物体也能正确烘焙间接光照
    void CopyLightMappingProperties()
    {
        MaterialProperty mainTex = FindProperty("_MainTex",properties,false);
        MaterialProperty baseMap = FindProperty("_BaseMap",properties,false);
        //将baseMap赋值给mainTex
        if (mainTex != null && baseMap != null)
        {
            mainTex.textureValue = baseMap.textureValue;
            mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
        }
        //将basecolor赋值给color
        MaterialProperty color = FindProperty("_Color",properties,false);
        MaterialProperty baseColor = FindProperty("_BaseColor",properties,false);
        if (color != null&&baseColor != null)
        {
            color.colorValue = baseColor.colorValue;
        }
    }

    void BakedEmission()
    {
        EditorGUI.BeginChangeCheck();
        //会显示出一个名为 “Global Illumination”（全局光照）的下拉菜单，默认值为 “None”,Bake时烘焙自发光
        editor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material m in editor.targets)
            {
                m.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
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

    void SetShadowCasterPass()
    {
        MaterialProperty shadows = FindProperty("_Shadows",properties,false);
        if (shadows == null || shadows.hasMixedValue)
        {
            return;
        }
        //有阴影时才启用ShadowCaster Pass
        bool enabled = shadows.floatValue < (float) ShadowMode.Off;
        foreach (Material m in materials)
        {
            m.SetShaderPassEnabled("ShadowCaster", enabled);
        }
    }
    

}
