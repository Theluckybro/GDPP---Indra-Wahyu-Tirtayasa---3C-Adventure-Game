using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ForestDetailSession
{
    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ForestHillGame.unity");
        if (AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Game/Resources/Level/ForestHillDetailedTerrain.asset") == null)
            ForestHillDetailAuthoring.Build();
        var group = GameObject.Find("ForestHillTerrainDetails");
        foreach (var child in group.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        if (!PrefabUtility.IsPartOfPrefabInstance(group))
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(group,"Assets/Game/Resources/Level/Prefabs/ForestHillTerrainDetails.prefab",InteractionMode.AutomatedAction,out bool success);
            if (!success) throw new Exception("Dressing prefab could not be saved.");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        var original = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Game/Resources/Level/ForestHillTerrain.asset");
        var detailed = GameObject.Find("ForestHillTerrain").GetComponent<Terrain>().terrainData;
        var originalPaint = original.GetAlphamaps(0,0,original.alphamapWidth,original.alphamapHeight);
        var detailPaint = detailed.GetAlphamaps(0,0,detailed.alphamapWidth,detailed.alphamapHeight);
        float maxDifference = 0;
        for (int z=0;z<original.alphamapHeight;z++) for(int x=0;x<original.alphamapWidth;x++) for(int l=0;l<original.alphamapLayers;l++) maxDifference = Mathf.Max(maxDifference, Mathf.Abs(originalPaint[z,x,l]-detailPaint[z,x,l]));
        File.AppendAllText("Library/DetailWork/detail-validation.txt", "Alpha max difference before correction=" + maxDifference + "\n");
        if (maxDifference > 0) throw new Exception("Saved terrain textures differ from the original.");
        Equal(original.GetHeights(0,0,original.heightmapResolution,original.heightmapResolution), detailed.GetHeights(0,0,detailed.heightmapResolution,detailed.heightmapResolution));
        Equal(original.GetAlphamaps(0,0,original.alphamapWidth,original.alphamapHeight), detailed.GetAlphamaps(0,0,detailed.alphamapWidth,detailed.alphamapHeight));
        Equal(original.GetHoles(0,0,original.holesResolution,original.holesResolution), detailed.GetHoles(0,0,detailed.holesResolution,detailed.holesResolution));
        if (detailed.detailPrototypes.Length != 4 || GameObject.Find("ForestHillTerrain").GetComponent<TerrainCollider>().terrainData != detailed) throw new Exception("Detail configuration or collider mismatch.");
        float terrainZ = GameObject.Find("ForestHillTerrain").transform.position.z;
        for (int layer=0;layer<4;layer++)
        {
            var prototype = detailed.detailPrototypes[layer];
            if (!prototype.Validate(out string error) || prototype.noiseSeed != 0 || prototype.noiseSpread != 20 || prototype.density != 3 || !prototype.useInstancing) throw new Exception("Invalid detail prototype: " + error);
            var map = detailed.GetDetailLayer(0,0,detailed.detailWidth,detailed.detailHeight,layer);
            long count=0;
            for(int z=0;z<detailed.detailHeight;z++) for(int x=0;x<detailed.detailWidth;x++)
            {
                count += map[z,x];
                float worldZ = terrainZ + (z+.5f)/detailed.detailHeight*detailed.size.z;
                if(worldZ < -31 && map[z,x] != 0) throw new Exception("Detail intrudes into island/glide region.");
            }
            if(count==0) throw new Exception("Empty saved detail layer.");
        }
        if (group.transform.childCount != 58) throw new Exception("Missing terrain decorations.");
        foreach(var t in group.GetComponentsInChildren<Transform>(true)) if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0) throw new Exception("Missing script in dressing.");
        File.AppendAllText("Library/DetailWork/detail-validation.txt", "PASS: exact height, texture and hole comparison; terrain/collider references match.\n");
        Preview("overview",new Vector3(-48,83,-99),new Vector3(24,10,-7),true,49);
        Preview("path",new Vector3(-3,13,-7),new Vector3(-2,11,10),false,40);
    }
    static void Equal(Array a, Array b)
    {
        if (a.Length != b.Length) throw new Exception("Terrain dimensions changed.");
        var other = b.GetEnumerator();
        foreach (var item in a) { other.MoveNext(); if (!item.Equals(other.Current)) throw new Exception("Terrain surface changed."); }
    }
    static ForestDetailSession() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Library/DetailWork/detail-request.txt";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var command = File.ReadAllText(request).Trim(); File.Delete(request);
        try
        {
            if (command == "build") typeof(ForestDetailSession).Assembly.GetType("ForestHillDetailAuthoring").GetMethod("Build").Invoke(null, null);
            if (command == "inspect")
            {
                var scene = SceneManager.GetActiveScene();
                var sb = new StringBuilder(scene.path + " dirty=" + scene.isDirty + "\n");
                EditorSceneManager.SaveScene(scene, "Temp/DetailBackup/LiveBefore.unity", true);
                foreach (var t in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                {
                    sb.AppendLine(t.name + " " + t.transform.position + " " + t.terrainData.size + " details=" + t.terrainData.detailPrototypes.Length);
                    foreach (var l in t.terrainData.terrainLayers) sb.AppendLine("layer " + l.name);
                }
                File.WriteAllText("Library/DetailWork/detail-inspect.txt", sb.ToString());
            }
            if (command == "render")
            {
                Preview("overview",new Vector3(-48,83,-99),new Vector3(24,10,-7),true,49);
                Preview("path",new Vector3(-3,13,-7),new Vector3(-2,11,10),false,40);
            }
            File.WriteAllText("Library/DetailWork/detail-status.txt", "OK " + command);
        }
        catch (Exception e) { File.WriteAllText("Library/DetailWork/detail-status.txt", e.ToString()); }
    }
    static void Preview(string name, Vector3 position, Vector3 target, bool ortho, float size)
    {
        var go = new GameObject("Detail preview") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>(); cam.transform.position = position; cam.transform.LookAt(target);
        cam.orthographic = ortho; cam.orthographicSize = size; cam.fieldOfView = 65; cam.nearClipPlane = .08f; cam.farClipPlane = 1800;
        var extra = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); extra.requiresDepthTexture = true; extra.requiresColorTexture = true;
        var rt = new RenderTexture(1440,1000,24); var old = RenderTexture.active; var tex = new Texture2D(1440,1000,TextureFormat.RGB24,false);
        try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0,0,1440,1000),0,0); tex.Apply(); File.WriteAllBytes("Library/DetailWork/detail-"+name+".png",tex.EncodeToPNG()); }
        finally { cam.targetTexture = null; RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go); }
    }
}
