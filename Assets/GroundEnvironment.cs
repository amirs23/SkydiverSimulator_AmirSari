using UnityEngine;
using System.Collections.Generic;

/// Attach to an empty GameObject (e.g. "Environment") sitting at the world origin.
///
/// Fills the world with a low-poly town surrounded by fields of trees so there's
/// a believable landscape to descend toward from 500m. Everything is placed up
/// front into one container — no runtime chunk streaming — so the whole world is
/// always present and never pops in no matter the altitude.
///
/// WORKFLOW: right-click the component header →
///   • "Generate Environment" — scatters all buildings + trees into a child
///     "EnvironmentBaked" object. Save the scene (Cmd+S) to keep them.
///   • "Clear Environment"    — removes them so you can regenerate with new settings.
///
/// VISIBILITY REQUIREMENTS (from Sari):
///   areaHalfExtent = 3000 → 6 km × 6 km world. The visible horizon from 500m
///   altitude is ~2.5 km; the 3 km map guarantees ground props fill the horizon
///   in every direction. cameraFarClip = 5000 covers the diagonal plus headroom.
///
///   Fog starts at 3200m (past the map edge) and ends at 5000m so it softens the
///   far horizon without hiding anything inside the map — visible from ANY height.
///
/// PERFORMANCE (Quest 2):
///   Buildings use Cube (12 tris). Trees use Cylinder trunk (160 tris) + Cube
///   canopy (12 tris) instead of Sphere (515 tris) — 3x fewer tris per tree.
///   All props share a small set of materials (enableInstancing = true) and are
///   flagged Static for GPU batching. Colliders are stripped.
public class GroundEnvironment : MonoBehaviour
{
    [Header("Area (world units, centred on this object)")]
    [Tooltip("Half-width of the entire populated region. 3000 = 6 km x 6 km, fills the visible horizon from 500m.")]
    public float areaHalfExtent = 3000f;
    [Tooltip("Half-width of the central town cluster.")]
    public float townHalfExtent = 350f;
    [Tooltip("Keep a clear circle around the centre (the landing area).")]
    public float landingClearRadius = 30f;

    [Header("Buildings (town centre)")]
    public int   buildingCount   = 350;
    public float buildingMinSize = 8f;
    public float buildingMaxSize = 24f;
    public float buildingMinHeight = 10f;
    public float buildingMaxHeight = 80f;
    public Color[] buildingColors = {
        new Color(0.72f, 0.72f, 0.75f),
        new Color(0.60f, 0.55f, 0.50f),
        new Color(0.80f, 0.78f, 0.70f),
        new Color(0.55f, 0.60f, 0.66f),
    };

    [Header("Trees (spread across the whole map outside the town)")]
    public int   treeCount      = 2500;
    public float treeMinHeight  = 4f;
    public float treeMaxHeight  = 12f;
    public Color trunkColor   = new Color(0.36f, 0.25f, 0.15f);
    public Color foliageColor = new Color(0.16f, 0.40f, 0.14f);

    [Header("Randomness")]
    [Tooltip("Same value always produces the same world layout.")]
    public int seed = 12345;

    [Header("Camera — visible from any height")]
    [Tooltip("Far clip plane raised at launch. 5000 covers the 3 km map + 500m spawn height with headroom.")]
    public bool  setCameraFarClip = true;
    public float cameraFarClip    = 5000f;

    [Header("Fog — horizon softening only, must NOT hide the environment")]
    [Tooltip("Enable linear distance fog. Start MUST be > areaHalfExtent so nothing inside the map is fogged.")]
    public bool  enableFog        = true;
    [Tooltip("Fog starts here. Default 3200 > areaHalfExtent 3000 — no props are fogged.")]
    public float fogStartDistance = 3200f;
    [Tooltip("Fog ends here. Set equal to cameraFarClip for a gradual fade at the world edge.")]
    public float fogEndDistance   = 5000f;
    public Color fogColor         = new Color(0.72f, 0.78f, 0.86f);

    const string BakedName = "EnvironmentBaked";

    Material[] _buildingMats;
    Material   _trunkMat, _foliageMat;

    void Start()
    {
        ApplyCameraSettings();

        // If never baked in the editor, generate at runtime so the scene still works.
        if (transform.Find(BakedName) == null)
            Build(transform);
    }

    void ApplyCameraSettings()
    {
        if (setCameraFarClip && Camera.main != null)
            Camera.main.farClipPlane = Mathf.Max(Camera.main.farClipPlane, cameraFarClip);

        if (enableFog)
        {
            RenderSettings.fog              = true;
            RenderSettings.fogMode          = FogMode.Linear;
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance   = fogEndDistance;
            RenderSettings.fogColor         = fogColor;
        }
    }

    [ContextMenu("Generate Environment")]
    void Generate()
    {
        ClearEnvironment();
        var container = new GameObject(BakedName);
        container.transform.SetParent(transform, false);
        Build(container.transform);
        ApplyCameraSettings();
        Debug.Log($"[GroundEnvironment] Generated {buildingCount} buildings + {treeCount} trees " +
                  $"in a {areaHalfExtent * 2f:F0}m x {areaHalfExtent * 2f:F0}m world. " +
                  "Save the scene (Cmd+S) to keep them.");
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
        m.SetFloat("_Metallic",   0f);
        m.enableInstancing = true;
        return m;
    }

    void BuildBuildings(Transform root, System.Random rng)
    {
        var parent = NewChild("Buildings", root).transform;
        for (int i = 0; i < buildingCount; i++)
        {
            var p = RandomInSquare(rng, townHalfExtent);
            if (p.magnitude < landingClearRadius) continue;

            float w = Lerp(rng, buildingMinSize, buildingMaxSize);
            float d = Lerp(rng, buildingMinSize, buildingMaxSize);
            float h = Lerp(rng, buildingMinHeight, buildingMaxHeight);

            var go = Prim(PrimitiveType.Cube, $"Bldg{i:D3}", parent);
            go.transform.localPosition = new Vector3(p.x, h * 0.5f, p.y);
            go.transform.localScale    = new Vector3(w, h, d);
            SetMat(go, _buildingMats[rng.Next(_buildingMats.Length)]);
            MarkStatic(go);
        }
    }

    void BuildTrees(Transform root, System.Random rng)
    {
        var parent = NewChild("Trees", root).transform;
        for (int i = 0; i < treeCount; i++)
        {
            var p = RandomInSquare(rng, areaHalfExtent);
            // Skip the town footprint and the landing clearing.
            if (Mathf.Abs(p.x) < townHalfExtent && Mathf.Abs(p.y) < townHalfExtent) continue;
            if (p.magnitude < landingClearRadius) continue;

            float h       = Lerp(rng, treeMinHeight, treeMaxHeight);
            float trunkH  = h * 0.40f;
            float canopyH = h - trunkH;

            var tree = NewChild($"Tree{i:D4}", parent);
            tree.transform.localPosition = new Vector3(p.x, 0f, p.y);

            // Trunk — Cylinder (160 tris)
            var trunk = Prim(PrimitiveType.Cylinder, "Trunk", tree.transform);
            trunk.transform.localScale    = new Vector3(h * 0.06f, trunkH * 0.5f, h * 0.06f);
            trunk.transform.localPosition = new Vector3(0f, trunkH * 0.5f, 0f);
            SetMat(trunk, _trunkMat);
            MarkStatic(trunk);

            // Canopy — Cube (12 tris) not Sphere (515 tris) — Quest 2 performance
            float fw    = h * 0.50f;
            var foliage = Prim(PrimitiveType.Cube, "Foliage", tree.transform);
            foliage.transform.localScale    = new Vector3(fw, canopyH, fw);
            foliage.transform.localPosition = new Vector3(0f, trunkH + canopyH * 0.5f, 0f);
            SetMat(foliage, _foliageMat);
            MarkStatic(foliage);

            MarkStatic(tree);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

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
        var col = go.GetComponent<Collider>();
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
