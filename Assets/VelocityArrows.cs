using UnityEngine;

// Attach to any GameObject (e.g. create "ArrowsManager" in scene root).
// Drag the canopy Rigidbody into the Inspector slot.
// Draws two arrows every frame in world space:
//   Cyan  — horizontal velocity direction and speed
//   Yellow — vertical velocity (descent = points down, climb = points up)
public class VelocityArrows : MonoBehaviour
{
    [Tooltip("Canopy Rigidbody — source of velocity data")]
    public Rigidbody canopyRigidbody;

    [Tooltip("World units per m/s — controls how long the arrows are")]
    public float arrowScale = 0.4f;

    public float lineWidth = 0.06f;

    public Color horizontalColor = Color.cyan;
    public Color verticalColor   = new Color(1f, 0.9f, 0f);  // yellow

    LineRenderer _horzShaft, _horzHead1, _horzHead2;
    LineRenderer _vertShaft, _vertHead1, _vertHead2;

    void Start()
    {
        _horzShaft = MakeLine("HorzShaft", horizontalColor);
        _horzHead1 = MakeLine("HorzHead1", horizontalColor);
        _horzHead2 = MakeLine("HorzHead2", horizontalColor);
        _vertShaft = MakeLine("VertShaft", verticalColor);
        _vertHead1 = MakeLine("VertHead1", verticalColor);
        _vertHead2 = MakeLine("VertHead2", verticalColor);
    }

    void LateUpdate()
    {
        if (canopyRigidbody == null) return;

        Vector3 origin = canopyRigidbody.position;
        Vector3 vel    = canopyRigidbody.linearVelocity;

        DrawArrow(_horzShaft, _horzHead1, _horzHead2,
                  origin, new Vector3(vel.x, 0f, vel.z) * arrowScale);

        DrawArrow(_vertShaft, _vertHead1, _vertHead2,
                  origin, new Vector3(0f, vel.y, 0f) * arrowScale);
    }

    void DrawArrow(LineRenderer shaft, LineRenderer head1, LineRenderer head2,
                   Vector3 from, Vector3 delta)
    {
        Vector3 to = from + delta;
        shaft.SetPosition(0, from);
        shaft.SetPosition(1, to);

        if (delta.magnitude < 0.01f)
        {
            head1.SetPosition(0, to); head1.SetPosition(1, to);
            head2.SetPosition(0, to); head2.SetPosition(1, to);
            return;
        }

        float   headLen = Mathf.Min(delta.magnitude * 0.35f, 0.6f);
        Vector3 dir     = delta.normalized;

        // Pick a perpendicular that is always well-defined
        Vector3 perp = Vector3.Cross(dir, Vector3.up);
        if (perp.magnitude < 0.01f)
            perp = Vector3.Cross(dir, Vector3.right);
        perp = perp.normalized;

        Vector3 backBase = to - dir * headLen;
        head1.SetPosition(0, to); head1.SetPosition(1, backBase + perp * headLen * 0.5f);
        head2.SetPosition(0, to); head2.SetPosition(1, backBase - perp * headLen * 0.5f);
    }

    LineRenderer MakeLine(string lineName, Color color)
    {
        var go = new GameObject(lineName);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth    = lineWidth;
        lr.endWidth      = lineWidth;
        lr.useWorldSpace = true;
        lr.material      = new Material(Shader.Find("Sprites/Default"));
        lr.startColor    = color;
        lr.endColor      = color;
        return lr;
    }
}
