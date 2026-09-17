using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only authoring. The saved TerrainData is used directly at runtime.
public static class ForestHillTerrainAuthoring
{
    const string ScenePath = "Assets/Game/Scenes/ForestHillGame.unity";
    const string LayerPath = "Assets/Game/Resources/Level/Terrain Layer/";
    static readonly HashSet<string> FloorNames = new HashSet<string>
    {
        "Cube", "Cube (1)", "Cube (2)", "Cube (6)", "Cube (7)",
        "Cube (11)", "Cube (12)", "Cube (13)", "Cube (14)",
        "Cube (16)", "Cube (20)", "Cube (21)", "Cube (22)",
        "Cube (23)", "Cube (27)", "Cylinder"
    };
    static readonly HashSet<string> StepNames = new HashSet<string>
    { "Cube (3)", "Cube (4)", "Cube (5)", "Cube (8)", "Cube (9)", "Cube (10)" };

    sealed class Patch
    {
        public MeshRenderer renderer;
        public Bounds bounds;
        public bool round;
        public Collider collider;
        public float SurfaceHeight(float x, float z)
        {
            Vector3 p = new Vector3(Mathf.Clamp(x, bounds.min.x + .001f, bounds.max.x - .001f), bounds.max.y + 1, Mathf.Clamp(z, bounds.min.z + .001f, bounds.max.z - .001f));
            if (round)
            {
                Vector2 offset = Vector2.ClampMagnitude(new Vector2(p.x - bounds.center.x, p.z - bounds.center.z), bounds.extents.x * .999f);
                p.x = bounds.center.x + offset.x;
                p.z = bounds.center.z + offset.y;
            }
            return collider.Raycast(new Ray(p, Vector3.down), out RaycastHit hit, bounds.size.y + 2) ? hit.point.y : bounds.max.y;
        }
        public float Distance(float x, float z)
        {
            if (round)
                return new Vector2(x - bounds.center.x, z - bounds.center.z).magnitude - bounds.extents.x;
            float dx = Mathf.Abs(x - bounds.center.x) - bounds.extents.x;
            float dz = Mathf.Abs(z - bounds.center.z) - bounds.extents.z;
            return new Vector2(Mathf.Max(dx, 0), Mathf.Max(dz, 0)).magnitude + Mathf.Min(Mathf.Max(dx, dz), 0);
        }
    }

    [MenuItem("Tools/Forest Hill/Rebuild Terrain From Blockout")]
    public static void RebuildFromMenu()
    {
        if (EditorUtility.DisplayDialog("Rebuild Forest Hill terrain", "Replace the terrain heights and painted textures using the current blockout? Existing manual terrain painting will be replaced. Blockout colliders and obstacles are retained.", "Rebuild", "Cancel"))
            Build();
    }

    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open ForestHillGame in Edit Mode first.");
        Terrain terrain = null;
        Transform blockout = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "ForestHillTerrain") terrain = root.GetComponent<Terrain>();
            if (root.name == "BlockoutLevel") blockout = root.transform;
        }
        if (terrain == null || terrain.terrainData == null || blockout == null)
            throw new InvalidOperationException("ForestHillTerrain or BlockoutLevel is missing.");

        var layers = new List<TerrainLayer>();
        foreach (string name in new[] { "Grass_Layer", "Dirt_Layer", "Dirt_Stone_Layer", "Forest_Layer", "Rock_Layer", "Cobblestone_Layer", "Ground_Sand_Tiled", "Sand_Layer", "SandRockyTrail", "StylizedSand" })
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath + name + ".terrainlayer");
            if (layer == null || layer.diffuseTexture == null)
                throw new InvalidOperationException("Missing terrain layer/texture: " + name);
            layers.Add(layer);
        }
        var patches = new List<Patch>();
        var steps = new List<Bounds>();
        foreach (var root in scene.GetRootGameObjects())
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.transform.IsChildOf(blockout) && StepNames.Contains(renderer.name)) steps.Add(renderer.bounds);
            if (!renderer.transform.IsChildOf(blockout) && renderer.name != "Cylinder") continue;
            if (renderer.gameObject.layer != LayerMask.NameToLayer("Ground") || !FloorNames.Contains(renderer.name)) continue;
            var collider = renderer.GetComponent<Collider>();
            if (collider == null || !collider.enabled) throw new InvalidOperationException("Missing floor collider: " + renderer.name);
            patches.Add(new Patch { renderer = renderer, bounds = renderer.bounds, round = renderer.name == "Cylinder", collider = collider });
        }
        if (patches.Count < 16) throw new InvalidOperationException("The expected blockout floors are missing.");

        // Save a copy, including previously unsaved edits, without changing the active scene path.
        Directory.CreateDirectory("Temp/ForestHillBackup");
        if (!File.Exists("Temp/ForestHillBackup/BeforeTerrain.unity"))
            EditorSceneManager.SaveScene(scene, "Temp/ForestHillBackup/BeforeTerrain.unity", true);
        var data = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, "Paint Forest Hill terrain");
        Undo.RecordObject(terrain.transform, "Position Forest Hill terrain");
        Undo.RecordObject(terrain.gameObject, "Set terrain ground layer");
        Vector3 origin = terrain.transform.position;
        origin.y = 0;
        Vector3 size = data.size;
        // Keep the user's horizontal placement and coverage; reserve 40 m vertically.
        size.y = 40;
        foreach (var patch in patches)
            if (patch.bounds.min.x < origin.x || patch.bounds.max.x > origin.x + size.x || patch.bounds.min.z < origin.z || patch.bounds.max.z > origin.z + size.z)
                throw new InvalidOperationException("Terrain must cover every blockout floor before rebuilding.");
        terrain.transform.position = origin;
        terrain.gameObject.layer = LayerMask.NameToLayer("Ground");
        data.heightmapResolution = 1025;
        data.alphamapResolution = 1024;
        data.size = size;
        data.terrainLayers = layers.ToArray();
        int res = data.heightmapResolution;
        var heights = new float[res, res];
        for (int z = 0; z < res; z++)
        for (int x = 0; x < res; x++)
        {
            float wx = origin.x + size.x * x / (res - 1f);
            float wz = origin.z + size.z * z / (res - 1f);
            float height = Height(patches, wx, wz);
            foreach (var step in steps)
                if (wx >= step.min.x - .12f && wx <= step.max.x + .12f && wz >= step.min.z - .12f && wz <= step.max.z + .12f)
                    height = Mathf.Min(height, step.max.y - .06f);
            heights[z, x] = height / size.y;
        }
        data.SetHeights(0, 0, heights);
        var holes = new bool[data.holesResolution, data.holesResolution];
        for (int z = 0; z < holes.GetLength(0); z++)
        for (int x = 0; x < holes.GetLength(1); x++) holes[z, x] = true;
        data.SetHoles(0, 0, holes);

        int a = data.alphamapResolution;
        var paint = new float[a, a, layers.Count];
        for (int z = 0; z < a; z++)
        for (int x = 0; x < a; x++)
        {
            float u = x / (a - 1f), v = z / (a - 1f);
            float wx = origin.x + size.x * u, wz = origin.z + size.z * v;
            Patch nearest = Nearest(patches, wx, wz, out float distance);
            float noise = Mathf.PerlinNoise(wx * .6f + 100, wz * .6f + 100);
            float dirt = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.65f, .12f, distance + (noise - .5f) * .3f));
            // Large landing island gets a small arrival clearing; its outer area remains grass.
            if (nearest.round && nearest.bounds.extents.x > 10)
            {
                float clearing = Vector2.Distance(new Vector2(wx, wz), new Vector2(16, -45));
                dirt *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.8f, 4.8f, clearing));
            }
            float rock = Mathf.InverseLerp(32, 65, data.GetSteepness(u, v)) * .82f * (1 - dirt);
            float forest = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.48f, .78f, Mathf.PerlinNoise(wx * .075f + 30, wz * .075f + 30))) * .22f * (1 - dirt - rock);
            bool cave = wx > 56 && wx < 60 && ((wz > 4 && wz < 16) || (wz > -20 && wz < -8));
            paint[z, x, 0] = 1 - dirt - rock - forest;
            paint[z, x, 1] = dirt * (cave ? .35f : 1);
            paint[z, x, 2] = dirt * (cave ? .65f : 0);
            paint[z, x, 3] = forest;
            paint[z, x, 4] = rock;
        }
        data.SetAlphamaps(0, 0, paint);
        // Retain solid, precisely aligned blockout colliders underneath the painted surface.
        // Walls, ceilings, stepping obstacles, climbable walls and punch gates stay visible.
        foreach (var patch in patches)
        {
            Undo.RecordObject(patch.renderer, "Show terrain instead of floor blockout");
            patch.renderer.enabled = false;
        }
        terrain.Flush();
        Physics.SyncTransforms();
        Validate(terrain, patches);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = terrain.gameObject;
        SceneView.RepaintAll();
    }

    static Patch Nearest(List<Patch> patches, float x, float z, out float distance)
    {
        Patch nearest = null;
        distance = float.PositiveInfinity;
        foreach (var patch in patches)
        {
            float d = patch.Distance(x, z);
            if (d < distance) { nearest = patch; distance = d; }
        }
        return nearest;
    }

    static float Height(List<Patch> patches, float x, float z)
    {
        Patch nearest = Nearest(patches, x, z, out float d);
        float top = nearest.SurfaceHeight(x, z) + .012f;
        if (d <= 0) return top;
        // Narrow the southern shoulders smoothly, leaving the glide valley open.
        float southern = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-18, -24, z));
        float margin = Mathf.Lerp(3.5f, .2f, southern);
        float slope = Mathf.Lerp(5, 2.2f, southern);
        float blend = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(margin, margin + slope, d));
        float lowland = (.6f + Mathf.PerlinNoise(x * .055f + 100, z * .055f + 100) * 1.2f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-22, -30, z)));
        return Mathf.Lerp(lowland, top, blend);
    }

    static void Validate(Terrain terrain, List<Patch> patches)
    {
        var report = new System.Text.StringBuilder("Forest Hill terrain validation\n");
        foreach (var patch in patches)
        {
            Vector3 point = patch.bounds.center;
            float actual = terrain.SampleHeight(point) + terrain.transform.position.y;
            if (Mathf.Abs(actual - patch.SurfaceHeight(point.x, point.z)) > .08f)
                throw new InvalidOperationException("Height mismatch at " + patch.renderer.name + ": " + actual);
            if (!patch.renderer.GetComponent<Collider>().enabled)
                throw new InvalidOperationException("Floor collider was disabled.");
            report.AppendLine(patch.renderer.name + " " + point.ToString("F2") + " top=" + actual.ToString("F3"));
        }
        float gap = terrain.SampleHeight(new Vector3(17, 0, -35));
        if (gap > .05f) throw new InvalidOperationException("Glide gap was filled.");
        report.AppendLine("PASS: floor heights, colliders, glide valley; grass is layer 0.");
        File.WriteAllText("Temp/foresthill-validation.txt", report.ToString());
        Debug.Log(report.ToString());
    }
}
