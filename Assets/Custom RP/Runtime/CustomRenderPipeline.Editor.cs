using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;

public partial class CustomRenderPipeline
{
    partial void InitializeForEditor();
#if UNITY_EDITOR

    partial void InitializeForEditor()
    {
        Lightmapping.SetDelegate(lightsDelegate);
    }

    //在管线销毁时也重置烘焙光照的代理
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Lightmapping.ResetDelegate();
    }

    //使用委托处理烘焙光照时使用的不正确的光照衰减方式
    private static Lightmapping.RequestLightsDelegate lightsDelegate =
        (Light[] lights, NativeArray<LightDataGI> output) =>
        {
            var lightData = new LightDataGI();
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                switch (light.type)
                {
                    case LightType.Directional:
                        var directionalLight = new DirectionalLight();
                        LightmapperUtils.Extract(light,ref directionalLight);
                        lightData.Init(ref directionalLight);
                        break;
                    case LightType.Point:
                        var pointLight = new PointLight();
                        LightmapperUtils.Extract(light,ref pointLight);
                        lightData.Init(ref pointLight);
                        break;
                    //聚光可以设置内角和衰减
                    case LightType.Spot:
                        var spotLight = new SpotLight();
                        LightmapperUtils.Extract(light,ref spotLight);
                        spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
                        spotLight.angularFalloff = AngularFalloffType.AnalyticAndInnerAngle;
                        lightData.Init(ref spotLight);
                        break;
                    //强制面光源为烘焙类型
                    case LightType.Area:
                        var rectangleLight = new RectangleLight();
                        LightmapperUtils.Extract(light,ref rectangleLight);
                        rectangleLight.mode = LightMode.Baked;
                        lightData.Init(ref rectangleLight);
                        break;
                    //默认不烘焙该光源
                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                }
                //平方衰减
                lightData.falloff = FalloffType.InverseSquared;
                output[i] = lightData;
            }
        };
    
#endif
}
