using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Scene authoring only. Does not rebuild or repaint the manually edited main terrain/cliffs.
public static class ForestHillIslandAuthoring
{
    const string DataPath = "Assets/Game/Resources/Level/Terrain/ForrestHillIslandTerrain.asset";
    const string Prefabs = "Assets/Game/Resources/Level/Prefabs/";
    const float SeaLevel = 8.2f;
    static readonly Vector2 Center = new Vector2(16, -55);
    static Terrain island;

    [MenuItem("Tools/Forest Hill/Rebuild Island and Sea")]
    public static void RebuildFromMenu()
    {
        if (EditorUtility.DisplayDialog("Rebuild island and sea", "Replace island heights, textures and the IslandCoastDressing group? Manual edits on the main ForestHillTerrain and ForestHillCliffs are retained.", "Rebuild", "Cancel")) Build();
    }

    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Game/Scenes/ForestHillGame.unity" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open ForestHillGame in Edit Mode.");
        var main = GameObject.Find("ForestHillTerrain").GetComponent<Terrain>();
        var islandObject = GameObject.Find("ForestHillIslandTerrain");
        if (islandObject == null) throw new InvalidOperationException("The existing ForestHillIslandTerrain was not found.");
        island = islandObject.GetComponent<Terrain>();
        TerrainData data = island.terrainData;
        if (!AssetDatabase.IsValidFolder("Assets/Game/Resources/Level/Terrain"))
            AssetDatabase.CreateFolder("Assets/Game/Resources/Level", "Terrain");
        string oldPath = AssetDatabase.GetAssetPath(data);
        if (oldPath != DataPath)
        {
            string error = AssetDatabase.MoveAsset(oldPath, DataPath);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        Undo.RegisterCompleteObjectUndo(data, "Shape island beach");
        Undo.RecordObject(island.transform, "Position island terrain");
        Undo.RecordObject(island, "Configure independent island terrain");
        Undo.RecordObject(islandObject, "Set island Ground layer");
        data.name = "ForrestHillIslandTerrain";
        island.transform.position = new Vector3(Center.x - 24, 6.31f, Center.y - 24);
        islandObject.layer = LayerMask.NameToLayer("Ground");
        island.allowAutoConnect = false;
        island.groupingID = 1;
        island.SetNeighbors(null, null, null, null);
        island.materialTemplate = main.materialTemplate;
        data.heightmapResolution = 513;
        data.alphamapResolution = 512;
        data.size = new Vector3(48, 10, 48);
        var layers = new TerrainLayer[3];
        string[] names = { "Sand_Layer", "Grass_Layer", "Ground_Sand_Tiled" };
        for (int i = 0; i < names.Length; i++)
        {
            layers[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Game/Resources/Level/Terrain Layer/" + names[i] + ".terrainlayer");
            if (layers[i] == null) throw new InvalidOperationException("Missing layer " + names[i]);
        }
        data.terrainLayers = layers;
        var heights = new float[513, 513];
        for (int z = 0; z < 513; z++)
        for (int x = 0; x < 513; x++)
        {
            float wx = island.transform.position.x + x * 48f / 512;
            float wz = island.transform.position.z + z * 48f / 512;
            Vector2 delta = new Vector2(wx, wz) - Center;
            float radius = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x);
            float edge = 22 + Mathf.Sin(angle * 3) * .55f + Mathf.Cos(angle * 5) * .25f;
            float falloff = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(14.7f, edge + 1.5f, radius));
            float y = Mathf.Lerp(10.62f, 6.31f, falloff);
            // Cover the former island surface without touching its manually edited TerrainData.
            if (radius < 19.5f)
                y = Mathf.Max(y, main.SampleHeight(new Vector3(wx, 0, wz)) + main.transform.position.y + .045f);
            heights[z, x] = (y - island.transform.position.y) / data.size.y;
        }
        data.SetHeights(0, 0, heights);
        var holes = new bool[data.holesResolution, data.holesResolution];
        for (int z = 0; z < holes.GetLength(0); z++)
        for (int x = 0; x < holes.GetLength(1); x++) holes[z, x] = true;
        data.SetHoles(0, 0, holes);
        var paint = new float[512, 512, 3];
        for (int z = 0; z < 512; z++)
        for (int x = 0; x < 512; x++)
        {
            float wx = island.transform.position.x + x * 48f / 511;
            float wz = island.transform.position.z + z * 48f / 511;
            float greenRadius = Vector2.Distance(new Vector2(wx, wz), new Vector2(18, -59));
            float noise = Mathf.PerlinNoise(wx * .28f + 60, wz * .28f + 60);
            float grass = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.2f, 8.8f, greenRadius + (noise - .5f) * 1.4f));
            float y = data.GetInterpolatedHeight(x / 511f, z / 511f) + island.transform.position.y;
            float wet = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(SeaLevel, SeaLevel + .7f, y))) * .25f;
            paint[z, x, 0] = (1 - grass) * (1 - wet);
            paint[z, x, 1] = grass;
            paint[z, x, 2] = (1 - grass) * wet;
        }
        data.SetAlphamaps(0, 0, paint);
        island.GetComponent<TerrainCollider>().terrainData = data;
        island.Flush();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);

        var old = GameObject.Find("IslandCoastDressing");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var dressing = new GameObject("IslandCoastDressing");
        Undo.RegisterCreatedObjectUndo(dressing, "Dress island and coastline");
        var water = Place(dressing.transform, "Water", "Sea", new Vector3(20, SeaLevel, -30), new Vector3(100, 1, 100));
        water.layer = LayerMask.NameToLayer("Water");
        ConfigureOcean(water);
        PrefabUtility.RecordPrefabInstancePropertyModifications(water);
        foreach (var c in water.GetComponentsInChildren<Collider>()) { c.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(c); }
        foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            var extra = camera.GetComponent<UniversalAdditionalCameraData>();
            if (extra == null) continue;
            Undo.RecordObject(extra, "Enable water depth and refraction");
            extra.requiresDepthTexture = true;
            extra.requiresColorTexture = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(extra);
        }

        var decor = new GameObject("Beach rocks and vegetation").transform;
        decor.SetParent(dressing.transform, false);
        Fit(decor, "WillowTree_01_Green", "Island willow", new Vector3(18, Ground(18, -60) + 4.5f, -60), new Vector3(8, 9, 8));
        Fit(decor, "Stone_Big_01", "West beach rock", new Vector3(10, Ground(10, -58) + .65f, -58), new Vector3(4.5f, 1.8f, 3.5f));
        Fit(decor, "Stone_Big_02", "East beach rock", new Vector3(23.5f, Ground(23.5f, -61) + .8f, -61), new Vector3(5.5f, 2, 4));
        Fit(decor, "Stone_Medium_01", "Arrival side rock", new Vector3(23, Ground(23, -53) + .3f, -53), new Vector3(2.3f, 1, 1.8f));
        for (int i = 0; i < 12; i++)
        {
            float a = i * 2.39996f;
            float x = 18 + Mathf.Cos(a) * (3 + i % 3);
            float z = -60 + Mathf.Sin(a) * (2.5f + i % 3);
            Fit(decor, i % 2 == 0 ? "Bush_01_01" : "Bush_02_01", "Island bush " + i, new Vector3(x, Ground(x,z) + .35f, z), new Vector3(1.7f, .8f, 1.5f), false);
        }
        for (int i = 0; i < 64; i++)
        {
            float a = i * 2.39996f;
            float r = Mathf.Sqrt((i + .5f) / 64) * 6.8f;
            float x = 18 + Mathf.Cos(a) * r, z = -59 + Mathf.Sin(a) * r;
            var grass = Place(decor, i % 2 == 0 ? "Grass_01" : "Grass_02", "Beach grass " + i, new Vector3(x, Ground(x,z) - .03f, z), Vector3.one * (.55f + i % 3 * .1f));
            foreach(var c in grass.GetComponentsInChildren<Collider>()) { c.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(c); }
        }
        var front = new GameObject("Forest Hill seaward foundations").transform;
        front.SetParent(dressing.transform, false);
        // Dress only exposed faces seen from the island, below the playable ledges.
        Fit(front, "Cliff_03", "Gliding ledge foundation", new Vector3(17, 11.75f, -30.25f), new Vector3(6.2f, 8.8f, 2.8f));
        for (int i = 0; i < 7; i++)
            Fit(front, i % 2 == 0 ? "Cliff_01" : "Cliff_02", "Southern foundation " + i, new Vector3(24.5f + i * 5.5f, 11.7f, -24), new Vector3(6.1f, 9, 3.3f));

        Physics.SyncTransforms();
        Validate();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = islandObject;
        SceneView.RepaintAll();
    }

    static float Ground(float x, float z) { return island.SampleHeight(new Vector3(x, 0, z)) + island.transform.position.y; }

    public static void ConfigureOcean(GameObject water)
    {
        const string path = "Assets/Game/Resources/Level/Materials/Waterplants/ForestHillOcean.mat";
        var renderer = water.GetComponent<Renderer>();
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material == null)
        {
            material = new Material(renderer.sharedMaterial) { name = "ForestHillOcean" };
            AssetDatabase.CreateAsset(material, path);
        }
        // Opaque deep water hides the rectangular submerged terrain tile boundaries.
        material.SetColor("_Deep_Color", new Color(0, .53f, .69f, 1));
        material.SetColor("_Shallow_Color", new Color(.31f, .675f, .812f, .92f));
        material.SetFloat("_Depth_Distance", .005f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        renderer.sharedMaterial = material;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
    }

    static GameObject Place(Transform parent, string prefab, string label, Vector3 position, Vector3 scale)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefab + ".prefab");
        if (source == null) throw new InvalidOperationException("Missing prefab " + prefab);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
        go.name = label; go.transform.position = position; go.transform.rotation = Quaternion.identity; go.transform.localScale = scale;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = 3; PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject); }
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        return go;
    }

    static void Fit(Transform parent, string prefab, string label, Vector3 center, Vector3 size, bool collision = true)
    {
        var go = Place(parent, prefab, label, Vector3.zero, Vector3.one);
        Bounds b = BoundsOf(go);
        go.transform.localScale = new Vector3(size.x / b.size.x, size.y / b.size.y, size.z / b.size.z);
        go.transform.position = center - BoundsOf(go).center;
        foreach (var c in go.GetComponentsInChildren<Collider>()) { c.enabled = collision; PrefabUtility.RecordPrefabInstancePropertyModifications(c); }
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    public static void Validate()
    {
        island = GameObject.Find("ForestHillIslandTerrain").GetComponent<Terrain>();
        if (island.gameObject.layer != 3 || island.terrainData.terrainLayers.Length != 3 || island.allowAutoConnect)
            throw new InvalidOperationException("Island layer/texture/neighbour settings are incorrect.");
        int checkedSamples = 0;
        for (int degrees = 0; degrees < 360; degrees += 10)
        {
            float a = degrees * Mathf.Deg2Rad;
            float previous = float.PositiveInfinity;
            for (float r = 14.8f; r <= 23.5f; r += .25f)
            {
                float x = Center.x + Mathf.Cos(a) * r, z = Center.y + Mathf.Sin(a) * r;
                float y = Ground(x,z);
                if(y > previous + .04f || previous < float.PositiveInfinity && previous-y > .23f) throw new InvalidOperationException("Abrupt beach slope.");
                previous = y; checkedSamples++;
            }
            if(previous >= SeaLevel) throw new InvalidOperationException("An island edge is above sea level.");
        }
        var coast = GameObject.Find("IslandCoastDressing").transform;
        int pathSamples = 0;
        for(float z = -31; z >= -51; z -= .5f)
        {
            float y = z > -40 ? Mathf.Lerp(16.7f, 11.3f, (-z-31)/9) : Ground(16,z)+.2f;
            foreach(var c in Physics.OverlapCapsule(new Vector3(16,y+.35f,z),new Vector3(16,y+1.45f,z),.3f,~0,QueryTriggerInteraction.Ignore))
                if(c.transform.IsChildOf(coast)) throw new InvalidOperationException("New decoration blocks glide/arrival: " + c.name);
            pathSamples++;
        }
        File.WriteAllText("Temp/island-validation.txt", "PASS: " + checkedSamples + " smooth beach samples, submerged terrain edges, " + pathSamples + " clear glide/arrival samples, Ground layer and independent terrain settings.\n");
    }
}
