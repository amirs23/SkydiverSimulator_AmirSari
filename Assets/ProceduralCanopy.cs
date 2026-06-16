using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural ram-air parachute canopy.
///
/// TWO PARTS:
///   • STATIC (bakeable into real saved objects): cells, slider, pilot chute,
///     and the internal A/B/C/D suspension lines. These never move relative to
///     the canopy, so they can be baked once and saved into the scene.
///   • DYNAMIC (always runtime): risers → shoulders, steering lines → hands.
///     These attach to avatar bones that move every frame, so they CANNOT be
///     baked — they are rebuilt and updated live each LateUpdate.
///
/// Setup:
///   1. Create empty GameObject in scene → Add Component → ProceduralCanopy.
///   2. Position it ~7 m above the Avatar's hips.
///   3. Drag Follow Target + the 4 bone slots (shoulders, hands) in the Inspector.
///
/// Baking into real objects (recommended):
///   • Fill in all the shape/colour values you want.
///   • Right-click the component header → "Bake Canopy (static parts → scene)".
///     This creates a child "CanopyBaked" object holding the cells, slider,
///     pilot chute and suspension lines as real, editable, saved geometry.
///   • Save the scene (Cmd+S). On Play, the static parts are NOT regenerated —
///     only the riser + steering cables are built and animated to the bones.
///   • Right-click → "Clear Baked Parts" to remove them and start over.
///
/// If you never bake, everything is generated at runtime as before (backward
/// compatible) — the static parts just won't be visible/editable in Edit mode.
///
/// NOTE: [DefaultExecutionOrder] forces this script's LateUpdate to run AFTER
/// the XSens plugin has applied the avatar pose for the frame. Without this the
/// riser/steering cables read stale (bind-pose) bone positions and appear to
/// "not connect" to the hands.
/// </summary>
[DefaultExecutionOrder(10000)]
public class ProceduralCanopy : MonoBehaviour
{
    // ── Canopy shape ──────────────────────────────────────────────────────────
    [Header("Canopy Shape")]
    [Tooltip("Number of cells. Typical sport canopy: 7 or 9.")]
    public int cellCount = 9;

    [Tooltip("Full span (wingtip to wingtip) in metres.")]
    public float span = 9f;

    [Tooltip("Chord depth (leading edge to trailing edge) in metres.")]
    public float chord = 2.2f;

    [Tooltip("Peak inflation height of each cell at its mid-width.")]
    public float cellHeight = 0.38f;

    [Tooltip("How much the wingtips rise above the centre (elliptical arc).")]
    public float arcHeight = 0.9f;

    // ── Cell colours ──────────────────────────────────────────────────────────
    [Header("Cell Colors  (left → right; auto-fills if array is shorter than cellCount)")]
    public Color[] cellColors = new Color[]
    {
        new Color(0.85f, 0.10f, 0.10f),   // red
        new Color(0.95f, 0.85f, 0.05f),   // yellow
        new Color(0.10f, 0.25f, 0.85f),   // blue
        new Color(0.95f, 0.95f, 0.95f),   // white
        new Color(0.10f, 0.65f, 0.15f),   // green
        new Color(0.95f, 0.95f, 0.95f),   // white
        new Color(0.10f, 0.25f, 0.85f),   // blue
        new Color(0.95f, 0.85f, 0.05f),   // yellow
        new Color(0.85f, 0.10f, 0.10f),   // red
    };

    // ── Follow target ─────────────────────────────────────────────────────────
    [Header("Follow Target")]
    [Tooltip("Drag the Avatar root (or any bone) here. The canopy stays at this position + offset.")]
    public Transform followTarget;
    [Tooltip("Offset above the follow target in world units. Y = how high above the avatar.")]
    public Vector3 followOffset = new Vector3(0f, 7f, 0f);

    // ── Bone references ───────────────────────────────────────────────────────
    // IMPORTANT: this XSens avatar has THREE parallel node hierarchies:
    //   • CamelCase bones (LeftCarpus, LeftShoulder, LeftElbow …) — the skeleton the
    //     mesh is actually SKINNED to. These move the visible body.
    //   • j* joints (jLeftWrist, jLeftShoulder …) — XSens reference joints that are
    //     NOT skinned and sit detached at the rig root (near the torso). Attaching
    //     cables here makes them stop short of the hands — this was the long-standing
    //     "steering lines don't reach the hands" bug.
    //   • p* palpation landmarks (pLeftTopOfHand …) — surface reference points.
    // We always resolve to the SKINNED bones below, ignoring any j*/non-skinned
    // assignment left over in the scene.
    [Header("Bones — drag from Avatar skeleton")]
    [Tooltip("Skinned shoulder bone. Leave empty to auto-find 'LeftShoulder'.")]
    public Transform leftShoulder;
    [Tooltip("Skinned shoulder bone. Leave empty to auto-find 'RightShoulder'.")]
    public Transform rightShoulder;
    [Tooltip("Left hand / toggle grip point. Use the SKINNED 'LeftCarpus' bone — NOT jLeftWrist.")]
    public Transform leftHand;
    [Tooltip("Right hand / toggle grip point. Use the SKINNED 'RightCarpus' bone — NOT jRightWrist.")]
    public Transform rightHand;

    // ── Line appearance ───────────────────────────────────────────────────────
    [Header("Suspension Lines")]
    public float lineWidth     = 0.006f;
    public Color suspColor     = Color.white;
    [Tooltip("Steering lines are coloured differently to distinguish them.")]
    public Color steeringColor = new Color(1f, 0.8f, 0f);   // yellow

    // ── Pilot chute ───────────────────────────────────────────────────────────
    [Header("Pilot Chute")]
    public float pilotRadius  = 0.16f;
    public float pilotLineLen = 1.8f;

    // ─────────────────────────────────────────────────────────────────────────
    // A / B / C / D chord fractions (0 = leading edge / nose, 1 = trailing edge / tail)
    // A & B lines converge to front slider corners; C & D to rear corners.
    static readonly float[] CF = { 0.18f, 0.38f, 0.62f, 0.82f };

    // Names of the auto-generated child containers.
    const string BakedName   = "CanopyBaked";    // static parts (bakeable / saved)
    const string DynamicName = "CanopyDynamic";  // riser + steering cables (runtime only)

    // ─────────────────────────────────────────────────────────────────────────
    // Runtime
    // ─────────────────────────────────────────────────────────────────────────
    float _sliderLocalY;   // local-Y of slider below the canopy origin
    float _slHW;           // slider half-width  (along span)
    float _slHD;           // slider half-depth  (along chord)

    // Dynamic cables, rebuilt every Play:
    readonly List<LineRenderer> _riserLines = new List<LineRenderer>();  // FL, FR, RL, RR
    readonly List<LineRenderer> _steerLines = new List<LineRenderer>();  // L×2, R×2

    [Header("Toggle Handles")]
    [Tooltip("Show a small steering-toggle grip in each hand, at the bottom of the steering lines.")]
    public bool showToggleHandles = true;
    [Tooltip("Colour of the toggle grips.")]
    public Color handleColor = new Color(0.05f, 0.05f, 0.05f);
    [Tooltip("Length of each grip (metres).")]
    public float handleLength = 0.13f;
    [Tooltip("Radius of each grip (metres).")]
    public float handleRadius = 0.022f;
    Transform _handleL, _handleR;

    // =========================================================================
    // LIFECYCLE
    // =========================================================================
    void Start()
    {
        InitSliderDims();
        ResolveBones();

        // Build the static parts at runtime ONLY if they were never baked.
        if (transform.Find(BakedName) == null)
            BuildStatic(transform);

        BuildDynamicLines();
        UpdateDynamicLines();   // initial positions so nothing sits at origin
    }

    // Bone references assigned in the Inspector can come back null at runtime on a
    // streamed/retargeted XSens avatar. If that happens, re-find them by name under
    // the follow target (the Avatar) so the cables still attach correctly.
    void ResolveBones()
    {
        // Prefer the SKINNED bones (CamelCase). Names are searched in priority order;
        // the j* fallbacks are detached reference joints used only if nothing better
        // is found. We search BY NAME first and only keep the Inspector assignment if
        // the search comes up empty, so a stale/wrong slot can't override the fix.
        leftShoulder  = ResolveSkinned(leftShoulder,  "LeftShoulder",  "LeftCollar",  "jLeftShoulder");
        rightShoulder = ResolveSkinned(rightShoulder, "RightShoulder", "RightCollar", "jRightShoulder");
        leftHand      = ResolveSkinned(leftHand,      "LeftCarpus",  "LeftHand",  "jLeftWrist");
        rightHand     = ResolveSkinned(rightHand,     "RightCarpus", "RightHand", "jRightWrist");
    }

    // Resolve a bone reference to the SKINNED bone that actually moves the visible mesh.
    //
    // This XSens avatar imports TWO copies of each bone:
    //   • the real skinned/animated bone, nested under Hips (e.g.
    //     Hips/.../LeftShoulder 1/LeftElbow/LeftCarpus 1) — note the " 1" suffix Unity
    //     adds to de-duplicate the name. These spread along X with the T-pose.
    //   • a flat reference node parented directly under the Avatar root (e.g.
    //     Avatar/LeftCarpus, Avatar/jLeftWrist) that sits at the torso and never moves.
    // We therefore match each candidate name ignoring a trailing " 1"/" 2"… and PREFER
    // the copy that has a "Hips" ancestor, so cables attach to the visible limb.
    Transform ResolveSkinned(Transform assigned, params string[] names)
    {
        foreach (var n in names)
        {
            var found = FindSkinnedBone(n);
            if (found != null) return found;
        }
        return assigned;                                  // nothing found — keep what we had
    }

    Transform FindSkinnedBone(string wanted)
    {
        Transform searchRoot = followTarget != null ? followTarget : transform;
        Transform anyMatch = null;
        foreach (var t in searchRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!NameMatches(t.name, wanted)) continue;
            if (HasAncestorNamed(t, "Hips")) return t;    // the real skinned bone — prefer it
            anyMatch ??= t;                               // remember a fallback (e.g. detached ref)
        }
        return anyMatch;
    }

    // True if 'actual' equals 'wanted', or equals 'wanted' with a trailing " <digits>"
    // that Unity appends to disambiguate duplicate node names ("LeftCarpus 1").
    static bool NameMatches(string actual, string wanted)
    {
        if (actual == wanted) return true;
        if (actual.Length > wanted.Length && actual.StartsWith(wanted) && actual[wanted.Length] == ' ')
        {
            for (int i = wanted.Length + 1; i < actual.Length; i++)
                if (!char.IsDigit(actual[i])) return false;
            return true;
        }
        return false;
    }

    static bool HasAncestorNamed(Transform t, string name)
    {
        for (Transform p = t.parent; p != null; p = p.parent)
            if (p.name == name) return true;
        return false;
    }

    void LateUpdate()
    {
        if (followTarget != null)
            transform.position = followTarget.position + followOffset;
        UpdateDynamicLines();
    }

    // =========================================================================
    // EDITOR BAKE  (right-click the component header in the Inspector)
    // =========================================================================
    [ContextMenu("Bake Canopy (static parts → scene)")]
    void BakeCanopy()
    {
        ClearBaked();
        InitSliderDims();

        var container = new GameObject(BakedName);
        container.transform.SetParent(transform, false);
        BuildStatic(container.transform);

        Debug.Log("[ProceduralCanopy] Baked static parts into '" + BakedName +
                  "'. Save the scene (Cmd+S) to keep them.");
    }

    [ContextMenu("Clear Baked Parts")]
    void ClearBaked()
    {
        var c = transform.Find(BakedName);
        if (c != null) SafeDestroy(c.gameObject);
    }

    // =========================================================================
    // STATIC PARTS  — cells, slider, pilot chute, suspension lines
    // Parented under 'root' (either the canopy itself at runtime, or the
    // CanopyBaked container when baking). All geometry is identical either way.
    // =========================================================================
    void BuildStatic(Transform root)
    {
        BuildCells(root);
        BuildSlider(root);
        BuildSuspensionLines(root);
        BuildPilotChute(root);
    }

    // =========================================================================
    // GEOMETRY HELPERS
    // =========================================================================
    void InitSliderDims()
    {
        _sliderLocalY = -(span * 0.30f);
        _slHW         = span  * 0.18f;
        _slHD         = chord * 0.22f;
    }

    // Vertical rise at span-position x due to the elliptical arc.
    // Centre = arcHeight (highest); wingtips = 0 (lowest) — inverted parabola.
    float ArcY(float x) => arcHeight * (1f - Mathf.Pow(x / (span * 0.5f), 2f));

    // Local-space attachment point for a suspension line:
    //   x         = span position of the rib
    //   chordFrac = 0 (nose) → 1 (tail)
    //   midHeight = true  → mid-cell height (for A/B/C/D suspension rows)
    //               false → lower-surface level (for steering line tail attachment)
    Vector3 AttachLocal(float x, float chordFrac, bool midHeight = true)
    {
        float z = -chord * chordFrac;
        float y = ArcY(x) + (midHeight ? cellHeight * 0.5f : 0f);
        return new Vector3(x, y, z);
    }

    // Local-space slider corner positions
    Vector3 SL_FL() => new Vector3(-_slHW, _sliderLocalY,  _slHD);
    Vector3 SL_FR() => new Vector3( _slHW, _sliderLocalY,  _slHD);
    Vector3 SL_RL() => new Vector3(-_slHW, _sliderLocalY, -_slHD);
    Vector3 SL_RR() => new Vector3( _slHW, _sliderLocalY, -_slHD);

    // =========================================================================
    // CELL MESHES
    // =========================================================================
    void BuildCells(Transform root)
    {
        float cellW = span / cellCount;

        for (int c = 0; c < cellCount; c++)
        {
            float xL  = -span * 0.5f + c * cellW;
            float xR  = xL + cellW;

            Color col = c < cellColors.Length
                      ? cellColors[c]
                      : Color.HSVToRGB((float)c / cellCount, 0.85f, 0.9f);

            var go = new GameObject($"Cell{c:D2}");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = MakeCellMesh(xL, xR);

            var mr  = go.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = col;
            mat.SetFloat("_Smoothness", 0.08f);
            mr.sharedMaterial   = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    // Builds one inflated cell mesh.
    // Upper surface: half-sine billow arch across cell width, tapered to 0 near nose & tail.
    // Lower surface: flat at arc height.
    // Nose cap and tail cap connect upper & lower at the leading / trailing edges.
    Mesh MakeCellMesh(float xL, float xR)
    {
        const int NW = 7;   // vertices across cell width  (left rib → right rib)
        const int ND = 6;   // vertices along chord        (nose → tail)

        float arcL = ArcY(xL);
        float arcR = ArcY(xR);

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs   = new List<Vector2>();
        var tris  = new List<int>();

        // ── Upper surface ─────────────────────────────────────────────────────
        int upperBase = 0;
        for (int d = 0; d < ND; d++)
        {
            float tv  = (float)d / (ND - 1);   // 0 = nose, 1 = tail
            float z   = -chord * tv;
            // Inflation: ramp up over first 20% of chord, full until 85%, taper to 0 at tail
            float inf = cellHeight
                      * Mathf.Clamp01(tv / 0.20f)
                      * Mathf.Clamp01((1f - tv) / 0.15f);

            for (int w = 0; w < NW; w++)
            {
                float tu   = (float)w / (NW - 1);
                float x    = Mathf.Lerp(xL, xR, tu);
                float arcX = Mathf.Lerp(arcL, arcR, tu);
                float y    = arcX + inf * Mathf.Sin(tu * Mathf.PI);   // billow arch

                verts.Add(new Vector3(x, y, z));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(tu, tv));
            }
        }

        // ── Lower surface ─────────────────────────────────────────────────────
        int lowerBase = verts.Count;
        for (int d = 0; d < ND; d++)
        {
            float tv = (float)d / (ND - 1);
            float z  = -chord * tv;

            for (int w = 0; w < NW; w++)
            {
                float tu   = (float)w / (NW - 1);
                float x    = Mathf.Lerp(xL, xR, tu);
                float arcX = Mathf.Lerp(arcL, arcR, tu);

                verts.Add(new Vector3(x, arcX, z));
                norms.Add(Vector3.down);
                uvs.Add(new Vector2(tu, tv));
            }
        }

        // ── Triangulate surfaces ──────────────────────────────────────────────
        AddQuadGrid(tris, upperBase, NW, ND, flip: false);   // upper faces up
        AddQuadGrid(tris, lowerBase, NW, ND, flip: true);    // lower faces down

        // ── Nose cap (d = 0): bridge upper ↔ lower ───────────────────────────
        AddBridgeStrip(tris, upperBase, lowerBase, NW, flip: false);

        // ── Tail cap (d = ND-1): bridge upper ↔ lower (reversed winding) ─────
        AddBridgeStrip(tris,
            upperBase + (ND - 1) * NW,
            lowerBase + (ND - 1) * NW,
            NW, flip: true);

        var mesh = new Mesh { name = "CellMesh" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Fill a NW×ND vertex grid with quads starting at 'baseIdx'.
    static void AddQuadGrid(List<int> tris, int baseIdx, int NW, int ND, bool flip)
    {
        for (int d = 0; d < ND - 1; d++)
        {
            for (int w = 0; w < NW - 1; w++)
            {
                int tl = baseIdx + d * NW + w;
                int tr = tl + 1;
                int bl = tl + NW;
                int br = bl + 1;

                if (!flip) { tris.Add(tl); tris.Add(tr); tris.Add(bl); tris.Add(tr); tris.Add(br); tris.Add(bl); }
                else       { tris.Add(tl); tris.Add(bl); tris.Add(tr); tris.Add(tr); tris.Add(bl); tris.Add(br); }
            }
        }
    }

    // Connect NW upper-row vertices to NW lower-row vertices with a strip of quads.
    static void AddBridgeStrip(List<int> tris, int upperRow, int lowerRow, int NW, bool flip)
    {
        for (int w = 0; w < NW - 1; w++)
        {
            int ut = upperRow + w,     ub = upperRow + w + 1;
            int lt = lowerRow + w,     lb = lowerRow + w + 1;

            if (!flip) { tris.Add(ut); tris.Add(ub); tris.Add(lt); tris.Add(ub); tris.Add(lb); tris.Add(lt); }
            else       { tris.Add(ut); tris.Add(lt); tris.Add(ub); tris.Add(ub); tris.Add(lt); tris.Add(lb); }
        }
    }

    // =========================================================================
    // SLIDER
    // =========================================================================
    void BuildSlider(Transform root)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Slider";
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0f, _sliderLocalY, -chord * 0.5f);
        go.transform.localScale    = new Vector3(_slHW * 2f, 0.012f, _slHD * 2f);
        SafeDestroy(go.GetComponent<BoxCollider>());

        var mr  = go.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.9f, 0.9f, 0.9f);
        mr.sharedMaterial = mat;
    }

    // =========================================================================
    // SUSPENSION LINES  (static — both ends live inside the canopy)
    // Built in LOCAL space so they ride rigidly with the canopy/baked container.
    // =========================================================================
    void BuildSuspensionLines(Transform root)
    {
        int   ribCount = cellCount + 1;
        float cellW    = span / cellCount;

        Vector3 slFL = SL_FL(), slFR = SL_FR(), slRL = SL_RL(), slRR = SL_RR();

        for (int r = 0; r < ribCount; r++)
        {
            float   ribX     = -span * 0.5f + r * cellW;
            bool    isLeft   = ribX <= 0f;
            Vector3 frontCor = isLeft ? slFL : slFR;
            Vector3 rearCor  = isLeft ? slRL : slRR;

            // A & B → front slider corner; C & D → rear slider corner
            MakeStaticLine(root, "SuspA", AttachLocal(ribX, CF[0]), frontCor);
            MakeStaticLine(root, "SuspB", AttachLocal(ribX, CF[1]), frontCor);
            MakeStaticLine(root, "SuspC", AttachLocal(ribX, CF[2]), rearCor);
            MakeStaticLine(root, "SuspD", AttachLocal(ribX, CF[3]), rearCor);
        }
    }

    // =========================================================================
    // PILOT CHUTE  (static — trails behind canopy tail in local space)
    // =========================================================================
    void BuildPilotChute(Transform root)
    {
        Vector3 tail  = new Vector3(0f, ArcY(0f), -chord);
        Vector3 pilot = tail - Vector3.forward * pilotLineLen;   // local +Z is canopy forward

        var line = MakeLR(root, "PilotLine", new Color(0.65f, 0.65f, 0.65f),
                          lineWidth * 0.8f, 2, worldSpace: false);
        line.SetPositions(new[] { tail, pilot });

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "PilotChute";
        go.transform.SetParent(root, false);
        go.transform.localPosition = pilot;
        go.transform.localScale    = Vector3.one * (pilotRadius * 2f);
        SafeDestroy(go.GetComponent<SphereCollider>());

        var mr  = go.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.7f, 0.7f, 0.7f);
        mr.sharedMaterial = mat;
    }

    // =========================================================================
    // DYNAMIC CABLES  — risers (→ shoulders) + steering lines (→ hands)
    // Rebuilt every Play, updated every LateUpdate. World-space.
    // =========================================================================
    void BuildDynamicLines()
    {
        // Clean up any stale runtime container (e.g. left over in a saved scene).
        var stale = transform.Find(DynamicName);
        if (stale != null) SafeDestroy(stale.gameObject);

        var container = new GameObject(DynamicName);
        container.transform.SetParent(transform, false);
        Transform root = container.transform;

        _riserLines.Clear();
        _steerLines.Clear();

        // 4 risers (front-L, front-R, rear-L, rear-R) — thicker
        for (int i = 0; i < 4; i++)
            _riserLines.Add(MakeLR(root, "Riser", suspColor, lineWidth * 2f, 2, worldSpace: true));

        // 4 steering lines (left×2, right×2) — 2 points: canopy underside → hand
        for (int i = 0; i < 4; i++)
            _steerLines.Add(MakeLR(root, "Steer", steeringColor, lineWidth, 2, worldSpace: true));

        if (showToggleHandles)
        {
            _handleL = MakeToggleHandle(root, "ToggleHandleL");
            _handleR = MakeToggleHandle(root, "ToggleHandleR");
        }
    }

    // A small cylindrical steering-toggle grip held in the hand at the bottom of the
    // steering lines. Positioned and oriented along the cable each frame (see UpdateDynamicLines).
    Transform MakeToggleHandle(Transform root, string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(root, false);
        // Unity's cylinder is 2 units tall along local Y; scale to handleLength × handleRadius.
        go.transform.localScale = new Vector3(handleRadius * 2f, handleLength * 0.5f, handleRadius * 2f);
        SafeDestroy(go.GetComponent<Collider>());
        var mr  = go.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = handleColor;
        mat.SetFloat("_Smoothness", 0.2f);
        mr.sharedMaterial = mat;
        return go.transform;
    }

    void UpdateDynamicLines()
    {
        if (_riserLines.Count < 4 || _steerLines.Count < 4) return;

        // Slider corners in world space
        Vector3 slFL = transform.TransformPoint(SL_FL());
        Vector3 slFR = transform.TransformPoint(SL_FR());
        Vector3 slRL = transform.TransformPoint(SL_RL());
        Vector3 slRR = transform.TransformPoint(SL_RR());

        // ── Risers → shoulders ──────────────────────────────────────────────
        Vector3 lSh = leftShoulder  ? leftShoulder.position
                    : transform.TransformPoint(new Vector3(-0.35f, _sliderLocalY - span * 0.25f, 0f));
        Vector3 rSh = rightShoulder ? rightShoulder.position
                    : transform.TransformPoint(new Vector3( 0.35f, _sliderLocalY - span * 0.25f, 0f));

        _riserLines[0].SetPositions(new[] { slFL, lSh });   // front-left
        _riserLines[1].SetPositions(new[] { slFR, rSh });   // front-right
        _riserLines[2].SetPositions(new[] { slRL, lSh });   // rear-left
        _riserLines[3].SetPositions(new[] { slRR, rSh });   // rear-right

        // ── Steering lines → hands ──────────────────────────────────────────
        // The canopy's wingspan runs along X but the avatar's hands sit on the Z
        // axis, so the canopy's own slider corners don't line up with the hands.
        // Rather than fight that, each steering line is a simple near-vertical cable:
        // the TOP sits on the canopy's lower surface directly above the hand, the
        // BOTTOM is the hand itself. When the hand pulls down the cable just shortens,
        // clearly showing the steering input. Two lines per hand, slightly spread.
        Vector3 lH = leftHand  ? leftHand.position  : lSh + Vector3.down * 0.55f;
        Vector3 rH = rightHand ? rightHand.position : rSh + Vector3.down * 0.55f;

        SetSteerCable(_steerLines[0], lH, -0.12f);
        SetSteerCable(_steerLines[1], lH,  0.12f);
        SetSteerCable(_steerLines[2], rH, -0.12f);
        SetSteerCable(_steerLines[3], rH,  0.12f);

        if (_handleL) PlaceToggleHandle(_handleL, lH);
        if (_handleR) PlaceToggleHandle(_handleR, rH);
    }

    // Sit the grip at the hand and align its long axis up along the steering line
    // (toward the canopy), so it reads as a toggle the hand is holding.
    void PlaceToggleHandle(Transform handle, Vector3 handWorld)
    {
        Vector3 toCanopy = transform.position - handWorld;
        if (toCanopy.sqrMagnitude < 1e-6f) toCanopy = Vector3.up;
        handle.position = handWorld;
        handle.rotation = Quaternion.FromToRotation(Vector3.up, toCanopy.normalized);
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    // One steering cable: top anchored on the canopy's lower surface directly above
    // the hand (offset slightly along the span so the pair reads as two lines), bottom
    // at the hand. xOffset is in canopy-local span units.
    void SetSteerCable(LineRenderer lr, Vector3 handWorld, float xOffset)
    {
        Vector3 hLoc = transform.InverseTransformPoint(handWorld);
        float   lx   = Mathf.Clamp(hLoc.x + xOffset, -span * 0.5f, span * 0.5f);
        float   lz   = Mathf.Clamp(hLoc.z, -chord, 0f);              // keep within the canopy footprint
        Vector3 top  = transform.TransformPoint(new Vector3(lx, ArcY(lx), lz));   // on the lower surface
        lr.SetPositions(new[] { top, handWorld });
    }

    // Static local-space line between two local-space points (2 waypoints).
    void MakeStaticLine(Transform root, string name, Vector3 aLocal, Vector3 bLocal)
    {
        var lr = MakeLR(root, name, suspColor, lineWidth, 2, worldSpace: false);
        lr.SetPositions(new[] { aLocal, bLocal });
    }

    LineRenderer MakeLR(Transform root, string name, Color col, float width, int pointCount, bool worldSpace)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);

        var lr               = go.AddComponent<LineRenderer>();
        lr.useWorldSpace     = worldSpace;
        lr.positionCount     = pointCount;
        lr.startWidth        = width;
        lr.endWidth          = width;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows    = false;

        var mat        = new Material(Shader.Find("Sprites/Default"));
        mat.color      = col;
        lr.sharedMaterial = mat;

        lr.SetPositions(new Vector3[pointCount]);
        return lr;
    }

    static void SafeDestroy(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else                       DestroyImmediate(o);
    }
}
