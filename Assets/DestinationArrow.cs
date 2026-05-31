using UnityEngine;

// Attach to any GameObject (e.g. "DestinationArrow") in the scene root.
// The arrow floats in front of the avatar and rotates to point at a target.
// When flying straight at the target you look almost straight down the shaft.
// When you steer off-course the arrow swings into view.
public class DestinationArrow : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The skydiver avatar transform — arrow is anchored relative to this")]
    public Transform avatar;

    [Tooltip("The destination to point at — drag any GameObject here")]
    public Transform destination;

    [Header("Placement")]
    [Tooltip("Offset from the avatar origin in avatar-local space (forward = +Z)")]
    public Vector3 localOffset = new Vector3(0f, -0.3f, 0.8f);

    [Header("Arrow shape")]
    [Tooltip("Length of the arrow shaft in metres")]
    public float shaftLength = 1.2f;

    [Tooltip("Line width in metres")]
    public float lineWidth = 0.07f;

    [Tooltip("Arrow head size as a fraction of shaft length")]
    [Range(0.1f, 0.5f)]
    public float headFraction = 0.3f;

    [Header("Appearance")]
    public Color arrowColor = new Color(1f, 0.85f, 0f); // bright yellow

    LineRenderer _shaft, _head1, _head2;

    void Start()
    {
        _shaft = MakeLine("Shaft");
        _head1 = MakeLine("Head1");
        _head2 = MakeLine("Head2");
    }

    void LateUpdate()
    {
        if (avatar == null || destination == null)
        {
            SetVisible(false);
            return;
        }
        SetVisible(true);

        // Anchor point: avatar position + local offset rotated by avatar's Y rotation only
        // (ignore pitch/roll so the arrow stays level with the horizon)
        Quaternion avatarYaw = Quaternion.Euler(0f, avatar.eulerAngles.y, 0f);
        Vector3 anchor = avatar.position + avatarYaw * localOffset;

        // Direction from anchor toward destination (horizontal only — keep arrow level)
        Vector3 toTarget = destination.position - anchor;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.01f)
        {
            SetVisible(false);
            return;
        }
        Vector3 dir = toTarget.normalized;

        Vector3 tip  = anchor + dir * shaftLength;
        Vector3 back = anchor + dir * shaftLength * (1f - headFraction);

        // Head barbs — perpendicular in the horizontal plane
        Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;
        float barbLen = shaftLength * headFraction * 0.5f;

        _shaft.SetPosition(0, anchor);
        _shaft.SetPosition(1, tip);
        _head1.SetPosition(0, tip);
        _head1.SetPosition(1, back + perp * barbLen);
        _head2.SetPosition(0, tip);
        _head2.SetPosition(1, back - perp * barbLen);
    }

    void SetVisible(bool on)
    {
        _shaft.enabled = on;
        _head1.enabled = on;
        _head2.enabled = on;
    }

    LineRenderer MakeLine(string lineName)
    {
        var go = new GameObject(lineName);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth    = lineWidth;
        lr.endWidth      = lineWidth;
        lr.useWorldSpace = true;
        lr.material      = new Material(Shader.Find("Sprites/Default"));
        lr.startColor    = arrowColor;
        lr.endColor      = arrowColor;
        return lr;
    }
}
