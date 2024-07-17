using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProbeVolume : MonoBehaviour
{
    public GameObject probePrefab;
    
    RenderTexture RT_WorldPos;
    RenderTexture RT_Normal;
    RenderTexture RT_Albedo;

    public int probeSizeX = 8;
    public int probeSizeY = 4;
    public int probeSizeZ = 8;
    public float probeGridSize = 2.0f;

    public ProbeVolumeData probeVolumeData;

    public ComputeBuffer coefficientVoxel;          // array for each probe's SH coefficient
    public ComputeBuffer lastFrameCoefficientVoxel; // last frame for inf bounce
    int[] cofficientVoxelClearValue;
    
    [Range(0.0f, 50.0f)]
    public float skyLightIntensity = 1.0f;

    [Range(0.0f, 50.0f)]
    public float GIIntensity = 1.0f;
    
    public GameObject[] probes;
    // Start is called before the first frame update
    void Start()
    {
        GenerateProbes();
        probeVolumeData.TryLoadSurfelData(this);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public void GenerateProbes()
    {
        //先清理之前的probes
        if(probes != null)
        {
            for(int i=0; i<probes.Length; i++)
            {
                DestroyImmediate(probes[i]);
            }
        }
        //清理computeBuffer
        if(coefficientVoxel != null) coefficientVoxel.Release();
        if(lastFrameCoefficientVoxel != null) lastFrameCoefficientVoxel.Release();

        
        int probeNum = probeSizeX * probeSizeY * probeSizeZ;

        // generate probe actors
        probes = new GameObject[probeNum];
        for(int x=0; x<probeSizeX; x++)
        {
            for(int y=0; y<probeSizeY; y++)
            {
                for(int z=0; z<probeSizeZ; z++)
                {
                    Vector3 relativePos = new Vector3(x, y, z) * probeGridSize;
                    Vector3 parentPos = gameObject.transform.position;

                    // setup probe
                    int index = x * probeSizeY * probeSizeZ + y * probeSizeZ + z;
                    probes[index] = Instantiate(probePrefab, gameObject.transform) as GameObject;
                    probes[index].transform.position = relativePos + parentPos; 
                    probes[index].GetComponent<Probe>().indexInProbeVolume = index;
                    probes[index].GetComponent<Probe>().TryInit();
                }
            }
        }
        // generate 1D "Voxel" buffer to storage SH coefficients
        coefficientVoxel = new ComputeBuffer(probeNum * 27, sizeof(int));
        lastFrameCoefficientVoxel = new ComputeBuffer(probeNum * 27, sizeof(int));
        cofficientVoxelClearValue = new int[probeNum *  27];
        for(int i=0; i<cofficientVoxelClearValue.Length; i++) 
        {
            cofficientVoxelClearValue[i] = 0;
        }  
    }
    
    // precompute surfel
    public void ProbeCapture()
    {
        // hide debug sphere
        foreach (var go in probes)
        {
            go.GetComponent<MeshRenderer>().enabled = false;
        }

        // cap
        foreach (var go in probes)
        {
            Probe probe = go.GetComponent<Probe>(); 
            probe.CaptureGbufferCubemaps();
        }

        probeVolumeData.StorageSurfelData(this);
    }
}
