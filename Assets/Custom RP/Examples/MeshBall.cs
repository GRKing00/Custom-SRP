using System;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public class MeshBall : MonoBehaviour
{
    private static int
        baseColorId = Shader.PropertyToID("_BaseColor"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness");

    [SerializeField]
    private Mesh mesh = default;
    
    [SerializeField]
    Material material = default;

    [SerializeField]
    private LightProbeProxyVolume lightProbeVolume = null;
    
    
    //每个实例的数据
    Matrix4x4[] matrices = new Matrix4x4[1023];
    Vector4[] baseColors = new Vector4[1023];
    
    float[] 
        metallic = new float[1023],
        smoothness = new float[1023];

    MaterialPropertyBlock block;

    void Awake()
    {
        //设置实例数据
        for (int i = 0; i < matrices.Length; i++)
        {
            //根据位置、旋转 、缩放获取矩阵
            matrices[i] = Matrix4x4.TRS(
                Random.insideUnitSphere * 10f,
                Quaternion.Euler(
                    Random.value * 360,Random.value * 360,Random.value * 360
                    ), 
                Vector3.one * Random.Range(0.5f, 1.5f)
                );
            //获取随机颜色值，alpha在0.5~1.0
            baseColors[i] = 
                new Vector4(
                    Random.value, Random.value, Random.value, 
                    Random.Range(0.5f, 1f)
                    );
            metallic[i] = Random.value < 0.25f ? 1f : 0f;
            smoothness[i] = Random.Range(0.05f, 0.95f);
        }
    }

    private void Update()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
            block.SetVectorArray(baseColorId, baseColors);
            block.SetFloatArray(metallicId, metallic);
            block.SetFloatArray(smoothnessId, smoothness);
            
            //  如果没有使用光照探针体积，则需要计算探针数据
            if (!lightProbeVolume)
            {
                //获取网格位置
                var positions = new Vector3[1023];
                for (int i = 0; i < matrices.Length; i++)
                {
                    positions[i] = matrices[i].GetColumn(3);
                }
                //二阶SH
                var lightProbes = new SphericalHarmonicsL2[1023];
                var occlusionProbes = new Vector4[1023];
                //计算获取当前位置的探针数据
                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(positions, lightProbes,occlusionProbes);
                //拷贝SH数据
                block.CopySHCoefficientArraysFrom(lightProbes);
                block.CopyProbeOcclusionArrayFrom(occlusionProbes);
            }

        }
        //绘制实例
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, 1023, block,
            ShadowCastingMode.On,true,0,null,
            lightProbeVolume?LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided,
            lightProbeVolume);
    }
}
