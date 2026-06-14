using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural ram-air parachute canopy — generates the full rigging system at runtime.
///
/// What gets built:
///   • N inflated cells (each a different colour), arced elliptically wingtip-to-wingtip
///   • Suspension lines: rows A, B → front slider corners; rows C, D → rear slider corners
///   • Slider: white rectangle that the lines converge through
///   • 4 risers: front-L, front-R, rear-L, rear-R → shoulders
///   • Steering lines: cascade from outer 2 cells each side → rear slider corners → toggles (hands)
///   • Pilot chute: small sphere trailing behind the canopy tail
///
/// Setup:
///   1. Create empty GameObject in scene → Add Component → ProceduralCanopy.
///   2. Position it ~7 m above the Avatar's hips.
///   3. Drag jLeftShoulder, jRightShoulder, jLeftHand, jRightHand into Inspector slots.
///   4. Play — everything is generated in code, no external assets needed.
///
/// To replace Canopy_Rotated + SuspensionLines:
///   • Disable or delete the old Canopy_Rotated GameObject.
///   • Disable SuspensionLines.cs on its GameObject (or delete it).
///   • Wire this component's bone slots instead.
/// </summary>
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

    // ── Bone references ───────────────────────────────────────────────────────
    [Header("Bones — drag from Avatar skeleton")]
    public Transform leftShoulder;
    public Transform rightShoulder;
    [Tooltip("Left hand / toggle grip point.")]
    public Transform leftHand;
    [Tooltip("Right hand / toggle grip point.")]
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

    // ─────────────────────────────────────────────────────────────────────────
    // Runtime
    // ─────────────────────────────────────────────────────────────────────────
    float _sliderLocalY;   // local-Y of slider below the canopy origin
    float _slHW;           // slider half-width  (along span)
    float _slHD;           // slider half-depth  (along chord)

    // All LineRenderers in build order:
    //   [0 … ribCount*4 - 1]  suspension lines (4 per rib: A, B, C, D)
    //   [ribCount*4 … +3]     riser lines (FL, FR, RL, RR)
    //   [+4 … +7]             steering lines (left×2, right×2)
    readonly List<LineRenderer> _lines = new List<LineRenderer>();
    int _riserStart;
    int _steerStart;

    LineRenderer _pilotLR;
    Transform    _pilotSphere;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        BuildCells();
        BuildSlider();
        BuildLines();
        BuildPilotChute();
        UpdateLines();   // set initial positions so nothing is at origin
    }

    void LateUpdate() => UpdateLines();

    // =========================================================================
    // GEOMETRY HELPERS
    // =========================================================================

    // Vertical rise at span-position x due to the elliptical arc.
    // Centre = 0 rise; wingtips = arcHeight.
    float ArcY(float x) => arcHeight * Mathf.Pow(x / (span * 0.5f), 2f);

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

    void BuildCells()
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
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().mesh = MakeCellMesh(xL, xR);

            var mr  = go.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = col;
            mat.SetFloat("_Smoothness", 0.08f);
            mr.material  = mat;
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

    void BuildSlider()
    {
        _sliderLocalY = -(span * 0.30f);
        _slHW         = span  * 0.18f;
        _slHD         = chord * 0.22f;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Slider";
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, _sliderLocalY, -chord * 0.5f);
        go.transform.localScale    = new Vector3(_slHW * 2f, 0.012f, _slHD * 2f);
        Destroy(go.GetComponent<BoxCollider>());

        var mr  = go.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.9f, 0.9f, 0.9f);
        mr.material = mat;
    }

    // =========================================================================
    // LINES — build all LineRenderers once
    // =========================================================================

    void BuildLines()
    {
        int ribCount = cellCount + 1;

        // 4 suspension lines per rib (A, B → front corner; C, D → rear corner)
        for (int r = 0; r < ribCount; r++)
            for (int i = 0; i < 4; i++)
                _lines.Add(MakeLR(suspColor, lineWidth, 2));

        // 4 riser lines (front-L, front-R, rear-L, rear-R)
        _riserStart = _lines.Count;
        for (int i = 0; i < 4; i++)
            _lines.Add(MakeLR(suspColor, lineWidth * 2f, 2));

        // 4 steering lines (left×2, right×2) — 3 waypoints each
        _steerStart = _lines.Count;
        for (int i = 0; i < 4; i++)
            _lines.Add(MakeLR(steeringColor, lineWidth, 3));
    }

    // =========================================================================
    // LINES — update positions every LateUpdate
    // =========================================================================

    void UpdateLines()
    {
        int   ribCount = cellCount + 1;
        float cellW    = span / cellCount;

        // Slider corners in world space
        Vector3 slFL = transform.TransformPoint(SL_FL());
        Vector3 slFR = transform.TransformPoint(SL_FR());
        Vector3 slRL = transform.TransformPoint(SL_RL());
        Vector3 slRR = transform.TransformPoint(SL_RR());

        // ── Suspension lines ──────────────────────────────────────────────────
        for (int r = 0; r < ribCount; r++)
        {
            float   ribX     = -span * 0.5f + r * cellW;
            bool    isLeft   = ribX <= 0f;
            Vector3 frontCor = isLeft ? slFL : slFR;
            Vector3 rearCor  = isLeft ? slRL : slRR;
            int     lineBase = r * 4;

            _lines[lineBase + 0].SetPositions(new[]
                { transform.TransformPoint(AttachLocal(ribX, CF[0])), frontCor });  // A
            _lines[lineBase + 1].SetPositions(new[]
                { transform.TransformPoint(AttachLocal(ribX, CF[1])), frontCor });  // B
            _lines[lineBase + 2].SetPositions(new[]
                { transform.TransformPoint(AttachLocal(ribX, CF[2])), rearCor });   // C
            _lines[lineBase + 3].SetPositions(new[]
                { transform.TransformPoint(AttachLocal(ribX, CF[3])), rearCor });   // D
        }

        // ── Risers ────────────────────────────────────────────────────────────
        Vector3 lSh = leftShoulder  ? leftShoulder.position
                    : transform.TransformPoint(new Vector3(-0.35f, _sliderLocalY - span * 0.25f, 0f));
        Vector3 rSh = rightShoulder ? rightShoulder.position
                    : transform.TransformPoint(new Vector3( 0.35f, _sliderLocalY - span * 0.25f, 0f));

        _lines[_riserStart + 0].SetPositions(new[] { slFL, lSh });   // front-left
        _lines[_riserStart + 1].SetPositions(new[] { slFR, rSh });   // front-right
        _lines[_riserStart + 2].SetPositions(new[] { slRL, lSh });   // rear-left
        _lines[_riserStart + 3].SetPositions(new[] { slRR, rSh });   // rear-right

        // ── Steering lines ────────────────────────────────────────────────────
        // Attach at trailing edge (chord fraction 1.0) of 2 outer cells each side.
        Vector3 lH = leftHand  ? leftHand.position  : lSh + Vector3.down * 0.55f;
        Vector3 rH = rightHand ? rightHand.position : rSh + Vector3.down * 0.55f;

        float[] stXL = { -span * 0.5f + 0.25f * cellW, -span * 0.5f + 1.25f * cellW };
        float[] stXR = {  span * 0.5f - 0.25f * cellW,  span * 0.5f - 1.25f * cellW };

        for (int i = 0; i < 2; i++)
        {
            Vector3 att = transform.TransformPoint(AttachLocal(stXL[i], 1f, midHeight: false));
            _lines[_steerStart + i].SetPositions(new[] { att, slRL, lH });
        }
        for (int i = 0; i < 2; i++)
        {
            Vector3 att = transform.TransformPoint(AttachLocal(stXR[i], 1f, midHeight: false));
            _lines[_steerStart + 2 + i].SetPositions(new[] { att, slRR, rH });
        }

        // ── Pilot chute ───────────────────────────────────────────────────────
        if (_pilotLR != null)
        {
            // Attaches at the tail centre (trailing edge, mid-arc height)
            Vector3 tail  = transform.TransformPoint(new Vector3(0f, ArcY(0f), -chord));
            // Trails directly behind the canopy (opposite to canopy forward)
            Vector3 pilot = tail - transform.forward * pilotLineLen;
            _pilotLR.SetPositions(new[] { tail, pilot });
            if (_pilotSphere != null) _pilotSphere.position = pilot;
        }
    }

    // =========================================================================
    // PILOT CHUTE
    // =========================================================================

    void BuildPilotChute()
    {
        _pilotLR = MakeLR(new Color(0.65f, 0.65f, 0.65f), lineWidth * 0.8f, 2);

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "PilotChute";
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * (pilotRadius * 2f);
        Destroy(go.GetComponent<SphereCollider>());

        var mr  = go.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.7f, 0.7f, 0.7f);
        mr.material = mat;

        _pilotSphere = go.transform;
    }

    // =========================================================================
    // HELPER — create a LineRenderer child
    // =========================================================================

    LineRenderer MakeLR(Color col, float width, int pointCount)
    {
        var go = new GameObject("LR");
        go.transform.SetParent(transform, false);

        var lr             = go.AddComponent<LineRenderer>();
        lr.useWorldSpace   = true;
        lr.positionCount   = pointCount;
        lr.startWidth      = width;
        lr.endWidth        = width;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows  = false;

        var mat        = new Material(Shader.Find("Sprites/Default"));
        mat.color      = col;
        lr.material    = mat;

        var zeros = new Vector3[pointCount];
        lr.SetPositions(zeros);
        return lr;
    }
}
