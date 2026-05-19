using UnityEngine;

// Attach to any GameObject. Place that GameObject on the ground at the target landing spot.
// Draws a pulsing orange bullseye (two rings + crosshair) visible from altitude.
// No meshes or prefabs required — everything is drawn with LineRenderers.
public class LandingZoneMarker : MonoBehaviour
{
    [Tooltip("Base colour of the target marker")]
    public Color markerColor = new Color(1f, 0.35f, 0f);

    [Tooltip("Outer ring radius (metres)")]
    public float outerRadius = 5f;

    [Tooltip("Inner ring radius (metres)")]
    public float innerRadius = 1.2f;

    [Tooltip("How fast the marker pulses (Hz)")]
    public float pulseSpeed = 1.2f;

    [Tooltip("Line width (metres)")]
    public float lineWidth = 0.15f;

    LineRenderer _outerRing, _innerRing, _crossH, _crossV;

    const int Segments = 64;

    void Start()
    {
        _outerRing = MakeRing("OuterRing", outerRadius);
        _innerRing = MakeRing("InnerRing", innerRadius);
        _crossH    = MakeCrossLine("CrossH", Vector3.right,   outerRadius);
        _crossV    = MakeCrossLine("CrossV", Vector3.forward, outerRadius);
    }

    void Update()
    {
        float alpha = 0.45f + 0.45f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f);
        Color c = new Color(markerColor.r, markerColor.g, markerColor.b, alpha);
        ApplyColor(_outerRing, c);
        ApplyColor(_innerRing, c);
        ApplyColor(_crossH,    c);
        ApplyColor(_crossV,    c);
    }

    LineRenderer MakeRing(string lineName, float radius)
    {
        var go = new GameObject(lineName);
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.loop         = true;
        lr.positionCount = Segments;
        lr.startWidth   = lineWidth;
        lr.endWidth     = lineWidth;
        lr.useWorldSpace = false;
        lr.material     = new Material(Shader.Find("Sprites/Default"));
        ApplyColor(lr, markerColor);

        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.02f, Mathf.Sin(angle) * radius));
        }
        return lr;
    }

    LineRenderer MakeCrossLine(string lineName, Vector3 direction, float size)
    {
        var go = new GameObject(lineName);
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth    = lineWidth;
        lr.endWidth      = lineWidth;
        lr.useWorldSpace = false;
        lr.material      = new Material(Shader.Find("Sprites/Default"));
        ApplyColor(lr, markerColor);

        lr.SetPosition(0, -direction * size + Vector3.up * 0.02f);
        lr.SetPosition(1,  direction * size + Vector3.up * 0.02f);
        return lr;
    }

    static void ApplyColor(LineRenderer lr, Color c)
    {
        lr.startColor = c;
        lr.endColor   = c;
    }
}
