using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;          // Drag the Avatar here in Inspector
    public Vector3 offset = new Vector3(0f, 2f, -6f);  // behind and slightly above
    public float smoothSpeed = 8f;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + target.rotation * offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1f);
    }
}
