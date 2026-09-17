using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class IslandSession
{
    public static void FinalizeBatch()
    {
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ForestHillGame.unity");
        ForestHillIslandAuthoring.ConfigureOcean(GameObject.Find("IslandCoastDressing/Sea"));
        Physics.SyncTransforms();
        ForestHillIslandAuthoring.Validate();
        Preview("island",new Vector3(-14,35,-89),new Vector3(17,8,-54),false,29);
        Preview("from-beach",new Vector3(16,12.3f,-56),new Vector3(27,16,-18),false,40);
        Preview("island",new Vector3(-14,35,-89),new Vector3(17,8,-54),false,29);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ForestHillGame.unity");
        Physics.SyncTransforms();
        ForestHillIslandAuthoring.Validate();
        Directory.CreateDirectory("Logs/IslandFinal");
        File.Copy("Temp/island-validation.txt","Logs/IslandFinal/validation.txt",true);
        File.Copy("Temp/coast-island.png","Logs/IslandFinal/island.png",true);
        File.Copy("Temp/coast-from-beach.png","Logs/IslandFinal/from-beach.png",true);
        Debug.Log("ISLAND FINALIZATION PASSED");
    }
    static IslandSession() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if(!File.Exists("Temp/island-request.txt") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var command=File.ReadAllText("Temp/island-request.txt").Trim(); File.Delete("Temp/island-request.txt");
        try
        {
            if(command=="build") ForestHillIslandAuthoring.Build();
            if(command=="verify") ForestHillIslandAuthoring.Validate();
            if(command=="water") { ForestHillIslandAuthoring.ConfigureOcean(GameObject.Find("IslandCoastDressing/Sea")); EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); }
            if(command=="reload") { var s=SceneManager.GetActiveScene(); if(s.isDirty) throw new Exception("Unsaved scene edits; reload skipped."); EditorSceneManager.OpenScene(s.path); Physics.SyncTransforms(); ForestHillIslandAuthoring.Validate(); }
            if(command=="render")
            {
                Preview("island", new Vector3(-14,35,-89),new Vector3(17,8,-54),true,29);
                Preview("overview",new Vector3(-65,100,-133),new Vector3(23,9,-22),true,70);
                Preview("from-beach",new Vector3(16,12.3f,-56),new Vector3(27,16,-18),false,40);
                Preview("glide",new Vector3(17,18,-27),new Vector3(16,10,-55),false,40);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
            if(command=="inspect")
            {
                var s=SceneManager.GetActiveScene(); var report=new StringBuilder(s.path+" dirty="+s.isDirty+"\n");
                EditorSceneManager.SaveScene(s,"Temp/IslandBackup/BeforeIsland.unity",true);
                foreach(var g in s.GetRootGameObjects())
                {
                    report.AppendLine(g.name+" position="+g.transform.position);
                    foreach(var t in g.GetComponentsInChildren<Terrain>()) report.AppendLine("TERRAIN "+t.name+" size="+t.terrainData.size+" data="+AssetDatabase.GetAssetPath(t.terrainData)+" layers="+t.terrainData.terrainLayers.Length);
                    if(g.name=="Cylinder") report.AppendLine("ISLAND BLOCK "+g.GetComponent<Renderer>().bounds);
                }
                File.WriteAllText("Temp/island-inspect.txt",report.ToString());
            }
            File.WriteAllText("Temp/island-status.txt","OK "+command);
        } catch(Exception e) { File.WriteAllText("Temp/island-status.txt",e.ToString()); }
    }
    static void Preview(string name,Vector3 position,Vector3 target,bool ortho,float size)
    {
        var go=new GameObject("Island preview"){hideFlags=HideFlags.HideAndDontSave};
        var cam=go.AddComponent<Camera>(); cam.transform.position=position; cam.transform.LookAt(target);
        cam.orthographic=ortho; cam.orthographicSize=size; cam.fieldOfView=65; cam.nearClipPlane=.08f; cam.farClipPlane=1800;
        var extra=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); extra.requiresDepthTexture=true; extra.requiresColorTexture=true;
        var rt=new RenderTexture(1440,1000,24); var old=RenderTexture.active; var tex=new Texture2D(1440,1000,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1440,1000),0,0);tex.Apply();File.WriteAllBytes("Temp/coast-"+name+".png",tex.EncodeToPNG());}
        finally{cam.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
