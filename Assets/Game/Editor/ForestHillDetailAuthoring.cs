using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bakes editable terrain detail layers and regular prefab instances; no runtime spawner.
public static class ForestHillDetailAuthoring
{
    const string Prefabs = "Assets/Game/Resources/Level/Prefabs/";
    const string DataPath = "Assets/Game/Resources/Level/ForestHillDetailedTerrain.asset";
    const string DressingPath = Prefabs + "ForestHillTerrainDetails.prefab";
    static readonly string[] DetailNames = { "Grass_01", "Grass_02", "Grass_03", "Flower_Red" };

    [MenuItem("Tools/Forest Hill/Add Main Terrain Details")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Game/Scenes/ForestHillGame.unity" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open ForestHillGame in Edit Mode first.");
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath) != null || GameObject.Find("ForestHillTerrainDetails") != null)
            throw new InvalidOperationException("Details already exist. Edit the saved detail layers with Paint Details and the dressing prefab in the Inspector.");
        var terrain = GameObject.Find("ForestHillTerrain").GetComponent<Terrain>();
        var source = terrain.terrainData;
        var prototypes = new DetailPrototype[DetailNames.Length];
        for (int i = 0; i < prototypes.Length; i++)
        {
            prototypes[i] = new DetailPrototype
            {
                prototype = LoadPrefab(DetailNames[i]), usePrototypeMesh = true,
                useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                minWidth = .5f, maxWidth = 1, minHeight = .5f, maxHeight = 1,
                noiseSeed = 0, noiseSpread = 20, density = 3,
                useDensityScaling = true, healthyColor = Color.white, dryColor = Color.white
            };
            if (!prototypes[i].Validate(out string error)) throw new InvalidOperationException(error);
        }
        // Clone the currently edited surface without modifying its heights, textures or original asset.
        if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), DataPath))
            throw new InvalidOperationException("Could not copy main terrain data.");
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
        data.name = "ForestHillDetailedTerrain";
        data.SetDetailResolution(512, 16);
        data.SetDetailScatterMode(DetailScatterMode.CoverageMode);
        data.detailPrototypes = prototypes;
        var paint = source.GetAlphamaps(0, 0, source.alphamapWidth, source.alphamapHeight);
        int grassIndex = Array.FindIndex(source.terrainLayers, l => l.name == "Grass_Layer");
        if (grassIndex < 0) throw new InvalidOperationException("Grass_Layer is missing.");
        Physics.SyncTransforms();
        var maps = new int[4][,];
        var totals = new long[4];
        for (int i = 0; i < maps.Length; i++) maps[i] = new int[512,512];
        for (int z = 0; z < 512; z++)
        for (int x = 0; x < 512; x++)
        {
            float u = (x + .5f) / 512, v = (z + .5f) / 512;
            Vector3 p = terrain.transform.position + new Vector3(u * data.size.x, data.GetInterpolatedHeight(u,v), v * data.size.z);
            if (!Eligible(terrain, paint, grassIndex, p, .58f)) continue;
            float blend = Mathf.PerlinNoise(p.x * .19f + 100, p.z * .19f + 100);
            maps[0][z,x] = Mathf.RoundToInt(Mathf.Lerp(100, 200, blend));
            maps[1][z,x] = Mathf.RoundToInt(Mathf.Lerp(75, 15, blend));
            maps[2][z,x] = Mathf.RoundToInt(Mathf.Lerp(12, 50, blend));
            float flowers = Mathf.PerlinNoise(p.x * .32f + 320, p.z * .32f + 180);
            maps[3][z,x] = flowers > .59f ? Mathf.RoundToInt((flowers - .59f) * 180) : 0;
            for (int i = 0; i < maps.Length; i++) totals[i] += maps[i][z,x];
        }
        for (int i = 0; i < maps.Length; i++)
        {
            if (totals[i] == 0) throw new InvalidOperationException("Empty detail layer: " + DetailNames[i]);
            data.SetDetailLayer(0,0,i,maps[i]);
        }
        Undo.RecordObject(terrain, "Add main terrain details");
        Undo.RecordObject(terrain.GetComponent<TerrainCollider>(), "Use detailed main terrain");
        terrain.terrainData = data;
        terrain.GetComponent<TerrainCollider>().terrainData = data;
        terrain.Flush();

        var group = new GameObject("ForestHillTerrainDetails");
        var occupied = new List<Vector3>();
        var random = new System.Random(1709);
        int trees = Dress(terrain, paint, grassIndex, group.transform, occupied, random,
            new[] { "BroadleafTree_01_Green", "BroadleafTree_03_Green", "WillowTree_01_Green" }, 16, 5.5f, 7.5f, 5f, 1.15f);
        int bushes = Dress(terrain, paint, grassIndex, group.transform, occupied, random,
            new[] { "Bush_01_01", "Bush_02_01", "Bush_03_01" }, 28, .65f, 1.1f, 1.8f, .45f);
        int rocks = Dress(terrain, paint, grassIndex, group.transform, occupied, random,
            new[] { "Rock_Small_01", "Rock_Medium_02", "Rock_Medium_03" }, 14, .55f, 1.2f, 1.8f, .5f);
        PrefabUtility.SaveAsPrefabAssetAndConnect(group, DressingPath, InteractionMode.AutomatedAction, out bool saved);
        if (!saved) throw new InvalidOperationException("Could not save terrain dressing prefab.");
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save ForestHillGame.");
        Selection.activeGameObject = terrain.gameObject;
        SceneView.RepaintAll();
        Debug.Log("PASS: four valid painted detail meshes; seed 0, spread 20, density 3, scale .5-1.\n" +
            "Layer coverage totals: " + string.Join(", ", totals) + "\nTrees=" + trees + " bushes=" + bushes + " rocks=" + rocks +
            "\nOriginal TerrainData retained; island excluded; heights/textures cloned unchanged.\n");
    }

    static bool Eligible(Terrain terrain, float[,,] paint, int grass, Vector3 p, float threshold)
    {
        var d = terrain.terrainData;
        float u = (p.x - terrain.transform.position.x) / d.size.x, v = (p.z - terrain.transform.position.z) / d.size.z;
        // The south island and glide gap are outside this main-land dressing pass.
        if (u <= 0 || u >= 1 || v <= 0 || v >= 1 || p.z < -31 || p.y < 8.8f) return false;
        if (d.GetSteepness(u,v) > 28 || d.IsHole(Mathf.Min(d.holesResolution-1,(int)(u*d.holesResolution)),Mathf.Min(d.holesResolution-1,(int)(v*d.holesResolution)))) return false;
        if (paint[(int)(v*(paint.GetLength(0)-1)),(int)(u*(paint.GetLength(1)-1)),grass] < threshold) return false;
        // Avoid vegetation underneath the existing cliffs, ceilings and other solid props.
        if (Physics.Raycast(p + Vector3.up * 35, Vector3.down, out RaycastHit hit, 36, ~0, QueryTriggerInteraction.Ignore) && hit.point.y > p.y + .35f) return false;
        return true;
    }

    static int Dress(Terrain terrain, float[,,] paint, int grass, Transform parent, List<Vector3> occupied,
        System.Random random, string[] names, int target, float minHeight, float maxHeight, float spacing, float clearance)
    {
        int placed = 0;
        for (int attempt = 0; attempt < 16000 && placed < target; attempt++)
        {
            Vector3 p = terrain.transform.position + new Vector3((float)random.NextDouble()*terrain.terrainData.size.x,0,(float)random.NextDouble()*terrain.terrainData.size.z);
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            if (!Eligible(terrain,paint,grass,p,.82f)) continue;
            bool valid = true;
            foreach (var prior in occupied) if (Vector3.Distance(prior,p) < spacing) { valid = false; break; }
            foreach (var dir in new[] { Vector3.left,Vector3.right,Vector3.forward,Vector3.back })
            {
                var edge = p + dir * clearance;
                edge.y = terrain.SampleHeight(edge) + terrain.transform.position.y;
                if (Mathf.Abs(edge.y-p.y) > .5f || !Eligible(terrain,paint,grass,edge,.68f)) { valid = false; break; }
            }
            if (!valid) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(names[placed % names.Length]),parent);
            foreach (var child in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(0,(float)random.NextDouble()*360,0);
            var bounds = BoundsOf(go);
            go.transform.localScale *= Mathf.Lerp(minHeight,maxHeight,(float)random.NextDouble()) / Mathf.Max(.01f,bounds.size.y);
            bounds = BoundsOf(go);
            go.transform.position += Vector3.up * (p.y-bounds.min.y-.04f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            occupied.Add(p); placed++;
        }
        return placed;
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    static GameObject LoadPrefab(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab");
        if (prefab == null) throw new InvalidOperationException("Missing prefab: " + name);
        return prefab;
    }
}
