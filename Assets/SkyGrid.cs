using UnityEngine;

// A grid of cloud clusters that follows the avatar by snapping,
// so clouds are always visible in every direction — same idea as the
// original sphere grid but with fluffy white cloud shapes.
public class SkyGrid : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Avatar transform the cloud grid follows")]
    public Transform avatarToFollow;

    [Header("Grid settings")]
    [Tooltip("Grid extends ±gridSize steps in X and Z (e.g. 5 = 11x11 = 121 clouds)")]
    public int gridSize = 5;

    [Tooltip("Distance between cloud centres (metres)")]
    public float spacing = 30f;

    [Tooltip("Fixed world-space Y height of the cloud layer")]
    public float cloudHeight = 200f;

    [Tooltip("Random Y offset per cloud within the layer thickness")]
    public float heightVariance = 8f;

    [Header("Cloud appearance")]
    [Tooltip("Puff spheres per cloud — more = fluffier")]
    [Range(3, 10)]
    public int puffsPerCloud = 4;

    [Tooltip("Base puff radius (metres)")]
    public float puffRadius = 3f;

    [Tooltip("Puff radius variance")]
    public float puffRadiusVariance = 4f;

    [Tooltip("How far puffs spread from the cloud centre")]
    public float puffSpread = 10f;

    [Tooltip("Cloud colour")]
    public Color cloudColor = Color.white;

    void Start()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = cloudColor;
        mat.SetFloat("_Smoothness", 0f);
        mat.SetFloat("_Metallic", 0f);

        for (int x = -gridSize; x <= gridSize; x++)
        {
            for (int z = -gridSize; z <= gridSize; z++)
            {
                float yOffset = Random.Range(-heightVariance, heightVariance);
                Vector3 localPos = new Vector3(x * spacing, yOffset, z * spacing);
                SpawnCloud(localPos, mat);
            }
        }
    }

    void SpawnCloud(Vector3 localPos, Material mat)
    {
        var cloud = new GameObject("Cloud");
        cloud.transform.SetParent(transform, false);
        cloud.transform.localPosition = localPos;

        for (int i = 0; i < puffsPerCloud; i++)
        {
            var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.transform.SetParent(cloud.transform, false);
            puff.transform.localPosition = Random.insideUnitSphere * puffSpread;

            float r = Mathf.Max(0.5f, puffRadius + Random.Range(-puffRadiusVariance, puffRadiusVariance));
            puff.transform.localScale = Vector3.one * r * 2f;

            puff.GetComponent<Renderer>().sharedMaterial = mat;
            Destroy(puff.GetComponent<Collider>());
        }
    }

    void Update()
    {
        if (avatarToFollow == null) return;

        // Follow avatar in XZ so clouds are always around you horizontally,
        // but Y is fixed so the layer stays at one altitude — you descend through it
        float snapX = Mathf.Round(avatarToFollow.position.x / spacing) * spacing;
        float snapZ = Mathf.Round(avatarToFollow.position.z / spacing) * spacing;
        transform.position = new Vector3(snapX, cloudHeight, snapZ);
    }
}
