using UnityEngine;

// DEV ONLY — tints all child renderers to a chosen color at startup.
// Add to the Canopy or Avatar root GameObject and set the color in the Inspector.
// Remove or disable before shipping.
public class DevColorize : MonoBehaviour
{
    public Color color = Color.cyan;

    void Start()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.materials)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                else if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            }
        }
    }
}
