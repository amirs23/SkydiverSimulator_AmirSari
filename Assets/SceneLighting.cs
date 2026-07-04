using UnityEngine;

/// One-stop daytime lighting setup for the skydiving scene.
///
/// The Default-Skybox is procedural: it needs a Sun Source to look right. This
/// component wires the directional light as the sun, gives it a warm angle +
/// soft shadows, and sets a gradient ambient so buildings/trees get pleasant
/// sky-lit shading instead of looking flat and gray.
///
/// USAGE (either works):
///   • Attach to any GameObject, drag your Directional Light into "Sun" (or leave
///     empty to auto-find one), then right-click the header -> "Apply Lighting"
///     and SAVE THE SCENE (Cmd+S). The settings persist in the scene's lighting.
///   • Or just leave it in the scene — it also re-applies on Play (Start).
///
/// Fog is left to GroundEnvironment.cs so the two don't fight.
[ExecuteAlways]
public class SceneLighting : MonoBehaviour
{
    [Header("Sun (directional light)")]
    [Tooltip("The scene's directional light. Auto-found if left empty.")]
    public Light sun;
    [Tooltip("Sun orientation (deg). X = height above horizon (50 = mid-morning), " +
             "Y = compass direction. Lower X = longer, more dramatic shadows.")]
    public Vector3 sunEuler = new Vector3(50f, -30f, 0f);
    [Tooltip("Sunlight colour — a touch warm for daylight.")]
    public Color sunColor = new Color(1.0f, 0.96f, 0.86f);
    [Range(0f, 3f)] public float sunIntensity = 1.3f;
    public bool softShadows = true;
    [Range(0f, 1f)] public float shadowStrength = 0.6f;

    [Header("Ambient (sky/horizon/ground gradient)")]
    [Tooltip("Use a 3-colour gradient ambient (nicer outdoors than flat skybox ambient).")]
    public bool useGradientAmbient = true;
    public Color skyColor     = new Color(0.55f, 0.70f, 0.95f);   // light blue overhead
    public Color equatorColor = new Color(0.55f, 0.68f, 0.55f);   // grass-tinted horizon haze
    public Color groundColor  = new Color(0.18f, 0.42f, 0.12f);   // grass green earth bounce (matches GrassGround + SkyboxGrass)
    [Range(0f, 2f)] public float ambientIntensity = 1.15f;

    void OnEnable()  => Apply();
    void Start()     => Apply();

    [ContextMenu("Apply Lighting")]
    public void Apply()
    {
        if (sun == null) sun = FindDirectionalLight();

        if (sun != null)
        {
            sun.type            = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(sunEuler);
            sun.color           = sunColor;
            sun.intensity       = sunIntensity;
            sun.shadows         = softShadows ? LightShadows.Soft : LightShadows.Hard;
            sun.shadowStrength  = shadowStrength;
            RenderSettings.sun  = sun;   // procedural skybox draws its sun disk here
        }

        if (useGradientAmbient)
        {
            RenderSettings.ambientMode      = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor      = skyColor     * ambientIntensity;
            RenderSettings.ambientEquatorColor  = equatorColor * ambientIntensity;
            RenderSettings.ambientGroundColor   = groundColor  * ambientIntensity;
        }
        else
        {
            RenderSettings.ambientMode      = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = ambientIntensity;
        }
    }

    static Light FindDirectionalLight()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) return l;
        return null;
    }
}
