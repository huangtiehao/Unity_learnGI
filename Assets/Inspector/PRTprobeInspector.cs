using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PRTprobe))]
public class PRTprobeInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Capture"))
        {
            PRTprobe probe = (PRTprobe)target;
            probe.Capture();
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
