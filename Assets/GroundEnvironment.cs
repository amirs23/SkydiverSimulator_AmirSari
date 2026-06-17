using UnityEngine;
using System.Collections.Generic;

/// Attach to an empty GameObject (e.g. "Environment") sitting at the world origin.
///
/// Fills the world with a low-poly town surrounded by fields of trees, so there's
/// a believable landscape to descend toward from 500m. Unlike a runtime chunk
/// streamer, EVERYTHING is placed up front into one container, so it's all visible
/// at once from altitude with no pop-in.
///
/// WORKFLOW (mirrors ProceduralCanopy's bake): right-click the component header →
///   • "Generate Environment" — scatters all buildings + trees into a child
///     "EnvironmentBaked" object. Save the scene (Cmd+S) to keep them.
///   • "Clear Environment"    — removes them so you can regenerate with new settings.
///
/// PERFORMANCE (Quest 2):
///   • All props reuse a small set of shared URP/Lit materials (instancing on) and
///     the built-in primitive meshes, and are flagged Static so Unity batches them.
///   • Colliders are stripped — scenery needs no physics.
///   • Change `seed` to get a different layout; counts/area are all tunable below.
public class GroundEnvironment : MonoBehaviour
{
    [Header("Area (world units, centred on this object)")]
    [Tooltip("Half-width of the whole populated square. 900 ≈ an 1800×1800 region.")]
    public float areaHalfExtent = 900f;
    [Tooltip("Half-width of the central town cluster (buildings live inside this).")]
    public float townHalfExtent = 250f;
    [Tooltip("Keep a clear circle around the centre (the landing area).")]
    public float landingClearRadius = 30f;

    [Header("Buildings (town)")]
    public int   buildingCount   = 280;
    public float buildingMinSize = 8f;    // footprint
    public float buildingMaxSize = 22f;
    public float buildingMinHeight = 10f;
    public float buildingMaxHeight = 70f;
    public Color[] buildingColors = {
        new Color(0.72f, 0.72f, 0.75f),
        new Color(0.60f, 0.55f, 0.50f),
        new Color(0.80f, 0.78f, 0.70f),
        new Color(0.55f, 0.60f, 0.66f),
    };

    [Header("Trees (fields)")]
    public int   treeCount      = 1400;
    public float treeMinHeight  = 4f;
    public float treeMaxHeight  = 11f;
    public Color trunkColor   = new Color(0.36f, 0.25f, 0.15f);
    public Color foliageColor = new Color(0.16f, 0.40f, 0.14f);

    [Header("Randomness")]
    [Tooltip("Change this for a different layout; same value reproduces the same world.")]
    public int seed = 12345;

    [Header("Camera (so the ground is visible from 500m)")]
    [Tooltip("Raise the main camera's far clip plane at launch so distant ground isn't culled.")]
    public bool  setCameraFarClip = true;
    public float cameraFarClip    = 3000f;
    [Tooltip("Add gentle distance fog so the far edge of the map fades into the horizon.")]
    public bool  enableFog = true;

    const string BakedName = "EnvironmentBaked";

    // Cache shared materials so all props of a kind batch together.
    Material[] _buildingMats;
    Material   _trunkMat, _foliageMat;

    void Start()
    {
        // One-shot, not per-frame: make sure tall views aren't clipped.
        if (setCameraFarClip && Camera.main != null)
            Camera.main.farClipPlane = Mathf.Max(Camera.main.farClipPlane, cameraFarClip);

        if (enableFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = areaHalfExtent * 0.6f;
            RenderSettings.fogEndDistance   = cameraFarClip;
        }

        // If never baked in the editor, build once at runtime so the scene still works.
        if (transform.Find(BakedName) == null)
            Build(transform);
    }

    [ContextMenu("Generate Environment")]
    void Generate()
    {
        ClearEnvironment();
        var container = new GameObject(BakedName);
        container.transform.SetParent(transform, false);
        Build(container.transform);
        Debug.Log($"[GroundEnvironment] Generated {buildingCount} buildings + {treeCount} trees " +
                  $"into '{BakedName}'. Save the scene (Cmd+S) to keep them.");
    }

    [ContextMenu("Clear Environment")]
    void ClearEnvironment()
    {
        var c = transform.Find(BakedName);
        if (c != null) SafeDestroy(c.gameObject);
    }

    void Build(Transform root)
    {
        BuildMaterials();
        var rng = new System.Random(seed);

        BuildBuildings(root, rng);
        BuildTrees(root, rng);
    }

    void BuildMaterials()
    {
        _buildingMats = new Material[buildingColors.Length];
        for (int i = 0; i < buildingColors.Length; i++)
            _buildingMats[i] = MakeMat(buildingColors[i]);
        _trunkMat   = MakeMat(trunkColor);
        _foliageMat = MakeMat(foliageColor);
    }

    Material MakeMat(Color c)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = c;
        m.SetFloat("_Smoothness", 0.05f);
        m.SetFloat("_Metallic", 0f);
        m.enableInstancing = true;
        return m;
    }

    void BuildBuildings(Transform root, System.Random rng)
    {
        Transform parent = NewChild("Buildings", root).transform;
        for (int i = 0; i < buildingCount; i++)
        {
            Vector2 p = RandomInSquare(rng, townHalfExtent);
            if (p.magnitude < landingClearRadius) continue; // keep landing area clear

            float w = Lerp(rng, buildingMinSize, buildingMaxSize);
            float d = Lerp(rng, buildingMinSize, buildingMaxSize);
            float h = Lerp(rng, buildingMinHeight, buildingMaxHeight);

            var go = Prim(PrimitiveType.Cube, $"Building{i:D3}", parent);
            go.transform.localPosition = new Vector3(p.x, h * 0.5f, p.y);
            go.transform.localScale    = new Vector3(w, h, d);
            SetMat(go, _buildingMats[rng.Next(_buildingMats.Length)]);
            MarkStatic(go);
        }
    }

    void BuildTrees(Transform root, System.Random rng)
    {
        Transform parent = NewChild("Trees", root).transform;
        for (int i = 0; i < treeCount; i++)
        {
            Vector2 p = RandomInSquare(rng, areaHalfExtent);
            // Skip the town footprint and the landing clearing.
            if (Mathf.Abs(p.x) < townHalfExtent && Mathf.Abs(p.y) < townHalfExtent) continue;
            if (p.magnitude < landingClearRadius) continue;

            float h = Lerp(rng, treeMinHeight, treeMaxHeight);
            float trunkH = h * 0.4f;
            float foliageH = h - trunkH;

            var tree = NewChild($"Tree{i:D4}", parent);
            tree.transform.localPosition = new Vector3(p.x, 0f, p.y);

            var trunk = Prim(PrimitiveType.Cylinder, "Trunk", tree.transform);
            // Unity cylinder is 2 units tall by default → scale Y by height/2.
            trunk.transform.localScale    = new Vector3(h * 0.06f, trunkH * 0.5f, h * 0.06f);
            trunk.transform.localPosition = new Vector3(0f, trunkH * 0.5f, 0f);
            SetMat(trunk, _trunkMat);
            MarkStatic(trunk);

            var foliage = Prim(PrimitiveType.Sphere, "Foliage", tree.transform);
            float fw = h * 0.45f;
            foliage.transform.localScale    = new Vector3(fw, foliageH, fw);
            foliage.transform.localPosition = new Vector3(0f, trunkH + foliageH * 0.5f, 0f);
            SetMat(foliage, _foliageMat);
            MarkStatic(foliage);

            MarkStatic(tree);
        }
    }

    // ---- helpers -----------------------------------------------------------

    Vector2 RandomInSquare(System.Random rng, float half) =>
        new Vector2((float)(rng.NextDouble() * 2 - 1) * half,
                    (float)(rng.NextDouble() * 2 - 1) * half);

    float Lerp(System.Random rng, float a, float b) =>
        Mathf.Lerp(a, b, (float)rng.NextDouble());

    GameObject NewChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    GameObject Prim(PrimitiveType type, string name, Transform parent)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        var col = go.GetComponent<Collider>();   // scenery needs no physics
        if (col != null) SafeDestroy(col);
        return go;
    }

    void SetMat(GameObject go, Material m)
    {
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = m;
    }

    void MarkStatic(GameObject go)
    {
#if UNITY_EDITOR
        UnityEditor.GameObjectUtility.SetStaticEditorFlags(go,
            UnityEditor.StaticEditorFlags.BatchingStatic |
            UnityEditor.StaticEditorFlags.OccluderStatic |
            UnityEditor.StaticEditorFlags.OccludeeStatic);
#endif
    }

    static void SafeDestroy(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
