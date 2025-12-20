using System;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public class MeshBall : MonoBehaviour
{
    static int baseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField]
    private Mesh mesh = default;
    
    [SerializeField]
    Material material = default;
    
    //每个实例的数据
    Matrix4x4[] matrices = new Matrix4x4[1023];
    Vector4[] baseColors = new Vector4[1023];

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
        }
    }

    private void Update()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
            block.SetVectorArray(baseColorId, baseColors);
        }
        //绘制实例
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, 1023, block);
    }
}
