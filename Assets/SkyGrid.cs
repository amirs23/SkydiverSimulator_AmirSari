using UnityEngine;

public class SkyGrid : MonoBehaviour
{
    [Header("Setup")]
    public GameObject spherePrefab;  // Drag your gridSphere prefab here
    public Transform avatarToFollow; // Drag your Avatar here

    [Header("Grid Settings")]
    public int gridSize = 5;         // 5 = 11x11 grid
    public float spacing = 10.0f;    // Distance between spheres

    void Start()
    {
        // Create the grid of spheres once at the start
        for (int x = -gridSize; x <= gridSize; x++)
        {
            for (int z = -gridSize; z <= gridSize; z++)
            {
                // Create a position relative to the center
                Vector3 pos = new Vector3(x * spacing, 0, z * spacing);
                
                // Create the sphere
                GameObject newSphere = Instantiate(spherePrefab, transform);
                newSphere.transform.localPosition = pos;
            }
        }
    }

    void Update()
    {
        // Make the whole grid follow the avatar, but "snap" to steps
        // This makes it look like you are moving PAST the spheres
        if (avatarToFollow != null)
        {
            float snapX = Mathf.Round(avatarToFollow.position.x / spacing) * spacing;
            float snapZ = Mathf.Round(avatarToFollow.position.z / spacing) * spacing;
            
            // We keep the Y (height) constant or follow player, depending on preference.
            // For now, let's keep it at the player's height:
            transform.position = new Vector3(snapX, avatarToFollow.position.y, snapZ);
        }
    }
}