using UnityEngine;

public class SuspensionLines : MonoBehaviour
{
    public Transform canopy;
    public Transform leftShoulder;
    public Transform rightShoulder;
    public float lineWidth = 0.06f;
    public float canopySpread = 0.8f; // horizontal spread of attachment points on canopy

    private LineRenderer leftLine;
    private LineRenderer rightLine;

    void Start()
    {
        leftLine  = CreateLine("LeftLine");
        rightLine = CreateLine("RightLine");
    }

    void LateUpdate()
    {
        if (canopy == null || leftShoulder == null || rightShoulder == null) return;

        // Use the visual center of the canopy mesh (not the root transform position,
        // which may be at y≈0 even when the mesh is high above)
        Vector3 canopyCenter = GetCanopyVisualCenter();

        Vector3 leftAttach  = canopyCenter + new Vector3(-canopySpread, 0, 0);
        Vector3 rightAttach = canopyCenter + new Vector3( canopySpread, 0, 0);
        UpdateLine(leftLine,  leftAttach,  leftShoulder.position);
        UpdateLine(rightLine, rightAttach, rightShoulder.position);
    }

    private Vector3 GetCanopyVisualCenter()
    {
        Renderer[] renderers = canopy.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return canopy.position;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        return b.center;
    }

    private LineRenderer CreateLine(string lineName)
    {
        GameObject lineObj = new GameObject(lineName);
        lineObj.transform.parent = this.transform;

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.useWorldSpace = true;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.yellow;
        lr.endColor = Color.yellow;

        return lr;
    }

    private void UpdateLine(LineRenderer lr, Vector3 from, Vector3 to)
    {
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
    }
}
