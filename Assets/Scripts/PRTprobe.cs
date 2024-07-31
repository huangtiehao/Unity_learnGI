using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;


struct Surfel
{
    
    public float4 albedo;
    public float4 worldPos;
    public float4 normalSkyMask;//最后一个float存skyMask
}
public class PRTprobe : MonoBehaviour
{
    const int sampleNum = 256;
    
    public RenderTexture normal;

    public RenderTexture worldPos;

    public RenderTexture albedo;

    public ComputeShader SampleSurfelShader;

    public ComputeShader RelightShader;

    private ComputeBuffer surfelBuffer;

    private ComputeBuffer radianceBuffer;
    
    private ComputeBuffer dirBuffer;
    
    private ComputeBuffer seedsBuffer;

    private ComputeBuffer SH9CoeffBuffer;
    
    

    private float[] seeds;
    
    private Surfel[] surfels;
    
    void calculateSH(double[] SH,float3 dir)
    {
        double PI = 3.14159265f;
        SH[0]=1.0/2.0*Math.Sqrt(1/PI);
        SH[1]=1.0/2.0*Math.Sqrt(3/PI)*dir.y;
        SH[2]=1.0/2.0*Math.Sqrt(3/PI)*dir.z;
        SH[3]=1.0/2.0*Math.Sqrt(3/PI)*dir.x;
        SH[4]=1.0/2.0*Math.Sqrt(15/PI)*dir.x*dir.y;
        SH[5]=1.0/2.0*Math.Sqrt(15/PI)*dir.y*dir.z;
        SH[6]=1.0/4.0*Math.Sqrt(5/PI)*(2*dir.z*dir.z-dir.x*dir.x-dir.y*dir.y);
        SH[7]=1.0/2.0*Math.Sqrt(15/PI)*dir.z*dir.x;
        SH[8]=1.0/4.0*Math.Sqrt(15/PI)*dir.x*dir.x-dir.y*dir.y;
    }

    public void setAllObjectsShader(Shader shader)
    {
        GameObject[] gameObjects = FindObjectsOfType(typeof(GameObject)) as GameObject[];
        foreach (var gameObject in gameObjects)
        {
            MeshRenderer meshRenderer= gameObject.GetComponent<MeshRenderer>();
            if(meshRenderer!=null) meshRenderer.sharedMaterial.shader = shader;
        }
    }
    public void Capture()
    {
        seeds = new float[sampleNum];
        Random.Range(0, 1);
        for (int i = 0; i < sampleNum; ++i)
        {
            seeds[i] = Random.value;
        }
        //初始化rendertexture
        normal ??= new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBFloat);
        worldPos ??= new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBFloat);
        albedo ??= new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBFloat);
        normal.dimension = TextureDimension.Cube;
        worldPos.dimension = TextureDimension.Cube;
        albedo.dimension = TextureDimension.Cube;
        
        //设置虚拟相机
        GameObject probeCamera = new GameObject("probeCamera");
        probeCamera.transform.position = transform.position;
        probeCamera.transform.rotation = transform.rotation;
        probeCamera.AddComponent<Camera>();
        
        Camera cameraComp = probeCamera.GetComponent<Camera>();
        cameraComp.clearFlags = CameraClearFlags.SolidColor;
        cameraComp.backgroundColor = new Color(0, 0, 0, 0);
        
        //遍历所有的物体设置shader
        //渲染到cubemap的gBuffer上
        setAllObjectsShader(Shader.Find("Unlit/captureAlbedo"));
        cameraComp.RenderToCubemap(albedo);
        
        setAllObjectsShader(Shader.Find("Unlit/captureNormal"));
        cameraComp.RenderToCubemap(normal);
        
        setAllObjectsShader(Shader.Find("Unlit/captureWorldPos"));
        cameraComp.RenderToCubemap(worldPos);
        
        //将shader设置回去
        setAllObjectsShader(Shader.Find("Universal Render Pipeline/Lit"));

        SampleSurfel();
        
        Relight();
        
        DestroyImmediate(probeCamera);
        

    }

    public void SampleSurfel()
    {
        SampleSurfelShader=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/SampleSurfel.compute");
        surfelBuffer ??= new ComputeBuffer(sampleNum,48);
        seedsBuffer ??= new ComputeBuffer(sampleNum,4);
        seedsBuffer.SetData(seeds);
        var kernel = SampleSurfelShader.FindKernel("CSMain");
        Vector4 probePos = gameObject.transform.position;
        SampleSurfelShader.SetBuffer(kernel,"seeds",seedsBuffer);
        SampleSurfelShader.SetVector("probePos",probePos);
        SampleSurfelShader.SetTexture(kernel,"albedo",albedo);
        SampleSurfelShader.SetTexture(kernel,"normal",normal);
        SampleSurfelShader.SetTexture(kernel,"worldPos",worldPos);
        SampleSurfelShader.SetBuffer(kernel, "surfels", surfelBuffer);
        surfels = new Surfel[sampleNum];
        SampleSurfelShader.Dispatch(kernel,1,1,1);
        
        surfelBuffer.GetData(surfels);
        // var Obj = new GameObject();
        // Obj.transform.position = new Vector3(0,0,0);
        // for (int i = 0; i < 256; ++i)
        // {
        //     Debug.Log(surfels[i].albedo+" : "+surfels[i].worldPos.xyz);
        //     var obj = new GameObject();
        //      obj.transform.position = surfels[i].worldPos.xyz;
        //      obj.transform.parent = Obj.transform;
        //     
        // }
        
        
    }

    public void Relight()
    {
        SH9CoeffBuffer ??= new ComputeBuffer(sampleNum * 9, 3*4);
        dirBuffer ??= new ComputeBuffer(sampleNum, 12);
        radianceBuffer ??= new ComputeBuffer(sampleNum, 12);
        
        RelightShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/Relight.compute");
        int kernelIndex = RelightShader.FindKernel("CSMain");
        RelightShader.SetVector("probePos",gameObject.transform.position);
        RelightShader.SetBuffer(kernelIndex,"dirs",dirBuffer);
        RelightShader.SetBuffer(kernelIndex,"SH9Coefficients",SH9CoeffBuffer);
        RelightShader.SetBuffer(kernelIndex,"radiances",radianceBuffer);
        surfelBuffer.SetData(surfels);
        RelightShader.SetBuffer(kernelIndex,"surfels",surfelBuffer);
        RelightShader.Dispatch(kernelIndex,1,1,1);
        
        
        float3[] AllSH9 = new float3[sampleNum * 9];
        float3[] dirs = new float3[sampleNum];
        float3[] radiances = new float3[sampleNum];
        SH9CoeffBuffer.GetData(AllSH9);
        dirBuffer.GetData(dirs);
        radianceBuffer.GetData(radiances);
        for (int i = 0; i < sampleNum * 9; ++i)
        {
            Debug.Log(AllSH9[i]);
        }
        Debug.Log(11111111);
        
        //球谐的系数
        float3[] li = new float3[9];
        for (int i = 0; i < sampleNum*9; ++i)
        {
            li[i % 9] += AllSH9[i]/sampleNum;
        }

        for (int i = 0; i < 9; ++i)
        {
            Debug.Log("li["+i+"]:"+li[i]);
        }
        for (int j = 0; j < 10; ++j)
        {
            double[] SHBasis = new double[9];
            calculateSH(SHBasis,dirs[j]);
            for (int i = 0; i < 9; ++i)
            {
                
                Debug.Log("Basis["+i+"]:"+SHBasis[i]);
                
            }
            float3 radiance = 0;
            for (int i = 0; i < 9; ++i)
            {
                radiance+=li[i]*(float)SHBasis[i];

            }
            Debug.Log("SH radiance:"+radiance+" \n real radiance:"+radiances[j]);
        }

        
        
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
