using UnityEngine;

// Attach to the Plane (ground) GameObject.
// Applies a grass-green URP/Lit material at runtime and scales the plane
// large enough that grass fills the horizon from any altitude.
public class GrassGround : MonoBehaviour
{
    [Tooltip("Base grass colour — applied to the ground plane")]
    public Color grassColor = new Color(0.18f, 0.42f, 0.12f);


    [Tooltip("How many times to scale up the default Unity plane (default plane = 10x10 units)")]
    public float planeScale = 2000f;

    void Start()
    {
        // Scale the plane so it extends well past the horizon
        transform.localScale = new Vector3(planeScale, 1f, planeScale);

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = grassColor;
            mat.SetFloat("_Smoothness", 0.05f);
            mat.SetFloat("_Metallic", 0f);
            rend.material = mat;
        }

    }
}
