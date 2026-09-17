using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Saves prefab instances into the scene; no runtime generation is required.
public static class ForestHillCliffAuthoring
{
    const string PrefabPath = "Assets/Game/Resources/Level/Prefabs/";
    const string RootName = "ForestHillCliffs";
    static int sequence;
    static Transform root;
    static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

    [MenuItem("Tools/Forest Hill/Rebuild Cliff Dressing")]
    public static void RebuildFromMenu()
    {
        if (EditorUtility.DisplayDialog("Rebuild cliff dressing", "Replace the ForestHillCliffs group with the authored cliff layout? Manual edits inside that group will be replaced.", "Rebuild", "Cancel")) Build();
    }

    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Game/Scenes/ForestHillGame.unity" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open ForestHillGame in Edit Mode.");
        prefabs.Clear();
        foreach (string name in new[] { "Cliff_01", "Cliff_02", "Cliff_03", "Cliff_04", "Cliff_05", "Stone_Climb" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + name + ".prefab");
            if (asset == null) throw new InvalidOperationException("Missing prefab: " + name);
            prefabs.Add(name, asset);
        }
        Directory.CreateDirectory("Temp/CliffBackup");
        if (!File.Exists("Temp/CliffBackup/BeforeCliffs.unity"))
            EditorSceneManager.SaveScene(scene, "Temp/CliffBackup/BeforeCliffs.unity", true);
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        sequence = 0;
        var container = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(container, "Dress Forest Hill with cliffs");
        root = container.transform;

        Transform start = Group("01 Start and lower area");
        // Keep the north opening clear where the start circle joins the corridor.
        for (int angle = 135; angle <= 405; angle += 45)
        {
            float a = angle * Mathf.Deg2Rad;
            Column(start, Mathf.Cos(a) * 8.8f, Mathf.Sin(a) * 8.8f, 0, 19 + (angle % 3), 6.4f, 6.4f);
        }
        for (float z = 7; z <= 20; z += 6.2f) Column(start, -7.3f, z, 0, 20.5f, 6, 7);
        Column(start, 7.5f, 9, 0, 19, 6, 6);

        Transform outer = Group("02 Outer cliffs");
        for (float x = 0; x <= 61; x += 6)
        {
            float floor = x < 15 ? 10.5f : x < 42 ? 13.5f : 16.5f;
            Column(outer, x, 26, 0, floor + 7 + Mathf.Sin(x) * 1.1f, 7.1f, 6.4f);
        }
        for (float z = 19; z >= -20; z -= 6)
            Column(outer, 66, z, 0, 24 + Mathf.Cos(z) * 1.2f, 7.4f, 7.2f);

        Transform inner = Group("03 Inner cliffs");
        for (float x = 14; x <= 49; x += 6)
            Column(inner, x, 10, 0, (x < 32 ? 21 : 24) + Mathf.Cos(x), 7.3f, 6.8f);
        for (float z = 3; z >= -7; z -= 6)
            Column(inner, 50, z, 0, 25, 7, 7.5f);
        for (float x = 22; x <= 49; x += 6)
            Column(inner, x, -10.5f, 0, 23.5f + Mathf.Sin(x), 7.2f, 6.4f);

        Transform steps = Group("04 Jump platforms");
        foreach (string name in new[] { "Cube (3)", "Cube (4)", "Cube (5)", "Cube (8)", "Cube (9)", "Cube (10)" })
        {
            var original = Block(name);
            Bounds b = original.GetComponent<Renderer>().bounds;
            // Use the original precise platform collider and dress it with a rock prefab.
            Rock(steps, "Cliff_04", new Bounds(b.center - Vector3.up * .015f, b.size), 0, 3, false, name + " stone platform");
            Hide(original.GetComponent<Renderer>());
        }
        // Rock skirt beneath the upper stepping area; the existing platform stays intact.
        for (float x = 33.5f; x <= 41; x += 3.5f)
        {
            Rock(steps, "Cliff_02", new Bounds(new Vector3(x, 6.2f, 14.4f), new Vector3(4, 12.5f, 2.1f)), 0);
            Rock(steps, "Cliff_03", new Bounds(new Vector3(x, 6.2f, 21.5f), new Vector3(4, 12.5f, 2.3f)), 180);
        }

        Transform caves = Group("05 Cave and sacred passage");
        Cave(caves, 4, 16, "Cave");
        Cave(caves, -20, -8, "Sacred");
        foreach (string name in new[] { "Cube (15)", "Cube (17)", "Cube (18)", "Cube (19)", "Cube (24)", "Cube (25)", "Cube (26)" })
            Hide(Block(name).GetComponent<Renderer>());
        // Match the south end wall of the sacred blockout, leaving its west exit open.
        Rock(caves, "Cliff_05", new Bounds(new Vector3(57.5f, 19.2f, -20.5f), new Vector3(3.2f, 5.4f, 3)), 0);

        Climb(Group("06 Climb wall and holds"));
        Column(outer, 48, -24, 0, 24, 7, 5.2f);
        Column(outer, 55, -24, 0, 25, 7.4f, 5.2f);

        Transform glide = Group("07 Gliding ledge and island");
        // Flank the peninsula, leaving its end and the complete flight corridor open.
        for (float z = -23; z >= -28; z -= 5)
        {
            Column(glide, 12, z, 0, 21 + Mathf.Sin(z), 4.4f, 5.5f);
            Column(glide, 22, z, 0, 21 + Mathf.Cos(z), 4.4f, 5.5f);
        }
        // Low rock foundation follows the landing island; its northern approach stays clear.
        for (int angle = 0; angle < 360; angle += 30)
        {
            float a = angle * Mathf.Deg2Rad;
            Rock(glide, angle % 60 == 0 ? "Cliff_01" : "Cliff_03", new Bounds(new Vector3(16 + Mathf.Cos(a) * 14.6f, 4.8f, -55 + Mathf.Sin(a) * 14.6f), new Vector3(8, 9.8f, 6)), (angle / 90) * 90);
        }
        Column(glide, 7, -67, 0, 15, 7, 6);
        Column(glide, 23, -67, 0, 16, 7, 6);

        Physics.SyncTransforms();
        Validate();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = container;
        SceneView.RepaintAll();
    }

    static Transform Group(string name)
    {
        var go = new GameObject(name); go.transform.SetParent(root, false); return go.transform;
    }

    static GameObject Block(string name)
    {
        var t = GameObject.Find("BlockoutLevel").transform.Find(name);
        if (t == null) throw new InvalidOperationException("Missing blockout: " + name);
        return t.gameObject;
    }

    static void Hide(Renderer renderer)
    {
        Undo.RecordObject(renderer, "Replace blockout appearance with cliffs");
        renderer.enabled = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
    }

    static void Column(Transform parent, float x, float z, float bottom, float top, float width, float depth)
    {
        float split = Mathf.Lerp(bottom, top, .62f);
        Rock(parent, "Cliff_02", new Bounds(new Vector3(x, (bottom + split) * .5f, z), new Vector3(width, split - bottom, depth)), (sequence % 4) * 90);
        Rock(parent, sequence % 3 == 0 ? "Cliff_01" : "Cliff_03", new Bounds(new Vector3(x, (split + top) * .5f - .55f, z), new Vector3(width * 1.04f, top - split + 1.1f, depth * 1.04f)), (sequence % 4) * 90);
    }

    static void Cave(Transform parent, float minZ, float maxZ, string label)
    {
        for (float z = minZ + 2; z < maxZ; z += 4)
        {
            if (label != "Sacred" || z > -17)
                Rock(parent, "Cliff_02", new Bounds(new Vector3(55, 18.9f, z), new Vector3(4, 5.8f, 4.6f)), 0, 3, true, label + " west wall");
            Rock(parent, "Cliff_02", new Bounds(new Vector3(61, 18.9f, z), new Vector3(4, 5.8f, 4.6f)), 180, 3, true, label + " east wall");
            Rock(parent, "Cliff_05", new Bounds(new Vector3(58, 20.6f, z), new Vector3(9.4f, 5.2f, 4.8f)), 0, 3, true, label + " roof");
        }
    }

    static void Climb(Transform parent)
    {
        var original = GameObject.Find("ClimbingWall");
        var collider = original.GetComponent<Collider>();
        Bounds wall = original.GetComponent<Renderer>().bounds;
        const int count = 5;
        float width = wall.size.x / count;
        for (int i = 0; i < count; i++)
        {
            float x = wall.min.x + width * (i + .5f);
            Rock(parent, i % 2 == 0 ? "Cliff_01" : "Cliff_02", new Bounds(new Vector3(x, wall.min.y * .5f, wall.center.z - 1.5f), new Vector3(width + .5f, wall.min.y + .2f, 3)), 0, 3, true, "Climb wall foundation");
            var rock = Rock(parent, "Cliff_02", new Bounds(new Vector3(x, wall.center.y, wall.center.z - .03f), new Vector3(width + .16f, wall.size.y, wall.size.z)), 0, 6, false, "Climbable cliff " + (i + 1));
            // Flat contiguous colliders keep the existing player's directional climb rays stable.
            var flat = rock.AddComponent<BoxCollider>();
            flat.center = rock.transform.InverseTransformPoint(new Vector3(x, wall.center.y, wall.center.z));
            flat.size = new Vector3(width / rock.transform.localScale.x, wall.size.y / rock.transform.localScale.y, wall.size.z / rock.transform.localScale.z);
            for (int row = 0; row < 6; row++)
            {
                float hx = x + (row % 2 == 0 ? -.55f : .55f);
                float hy = wall.min.y + .45f + row * .76f;
                Rock(parent, "Stone_Climb", new Bounds(new Vector3(hx, hy, wall.max.z + .04f), new Vector3(.34f, .20f, .22f)), 0, 0, false, "Stone Climb " + (i * 6 + row + 1));
            }
        }
        Rock(parent, "Cliff_05", new Bounds(new Vector3(wall.center.x, wall.max.y + .48f, wall.center.z), new Vector3(wall.size.x + 1, .96f, 1.6f)), 0, 3, true, "Upper non-climbable cap");
        Rock(parent, "Cliff_05", new Bounds(new Vector3(wall.center.x, wall.min.y - .45f, wall.center.z), new Vector3(wall.size.x + 1, .9f, 1.6f)), 0, 3, true, "Lower non-climbable cap");
        Hide(original.GetComponent<Renderer>());
        Undo.RecordObject(collider, "Replace climbing proxy with flat cliff colliders");
        collider.enabled = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
    }

    static GameObject Rock(Transform parent, string prefab, Bounds target, float yaw, int layer = 3, bool collision = true, string label = null)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefab], parent);
        go.name = (label ?? prefab) + " " + (++sequence).ToString("D3");
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, yaw, 0));
        go.transform.localScale = Vector3.one;
        Bounds b = RenderBounds(go);
        bool swap = Mathf.RoundToInt(yaw) % 180 != 0;
        go.transform.localScale = swap ? new Vector3(target.size.z / b.size.z, target.size.y / b.size.y, target.size.x / b.size.x) : new Vector3(target.size.x / b.size.x, target.size.y / b.size.y, target.size.z / b.size.z);
        go.transform.position += target.center - RenderBounds(go).center;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = layer;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
        }
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = collision;
            PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        return go;
    }

    static Bounds RenderBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    public static void Validate()
    {
        var container = GameObject.Find(RootName);
        int holds = 0, faces = 0;
        foreach (var t in container.GetComponentsInChildren<Transform>())
        {
            if (t.name.StartsWith("Stone Climb"))
            {
                holds++;
                foreach (var c in t.GetComponentsInChildren<Collider>())
                    if (c.enabled) throw new InvalidOperationException("Stone Climb collider must be disabled.");
            }
            if (t.name.StartsWith("Climbable cliff"))
            {
                faces++;
                if (t.gameObject.layer != 6 || !t.GetComponent<BoxCollider>().enabled) throw new InvalidOperationException("Cliff must have a flat Climbable collider.");
            }
        }
        if (holds != 30 || faces != 5) throw new InvalidOperationException("Incomplete climb dressing.");
        int rays = 0;
        for (float x = 29.2f; x < 42; x += .4f)
        for (float y = 16.7f; y < 21.5f; y += .4f)
        {
            if (!Physics.Raycast(new Vector3(x, y, -19.2f), Vector3.back, out RaycastHit hit, 1, 1 << 6) || Vector3.Dot(hit.normal, Vector3.forward) < .999f)
                throw new InvalidOperationException("Climb face gap at " + new Vector2(x, y));
            rays++;
        }
        if (Physics.Raycast(new Vector3(35.5f, 21.7f, -19.2f), Vector3.back, 1, 1 << 6) || Physics.Raycast(new Vector3(35.5f, 16.3f, -19.2f), Vector3.back, 1, 1 << 6))
            throw new InvalidOperationException("Climbable area extends beyond its caps.");
        File.WriteAllText("Temp/cliff-validation.txt", "PASS: " + rays + " continuous flat climb rays; 30 non-colliding holds; 5 climbable cliff faces; upper/lower climb limits.\n");
    }
}
