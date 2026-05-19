using UnityEngine;

public class SuspensionLines : MonoBehaviour
{
    public Transform canopy;
    public Transform leftShoulder;
    public Transform rightShoulder;
    public float lineWidth = 0.02f;
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

        Vector3 leftAttach  = canopy.position + new Vector3(-canopySpread, 0, 0);
        Vector3 rightAttach = canopy.position + new Vector3( canopySpread, 0, 0);
        UpdateLine(leftLine,  leftAttach,  leftShoulder.position);
        UpdateLine(rightLine, rightAttach, rightShoulder.position);
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
        lr.startColor = Color.white;
        lr.endColor = Color.white;

        return lr;
    }

    private void UpdateLine(LineRenderer lr, Vector3 from, Vector3 to)
    {
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
    }
}
