using UnityEngine;

// Attach to any GameObject (e.g. create "WindEffect" in scene root).
// Drag the Avatar transform into the Inspector slot.
// Creates a ring of small cloud-like spheres that drift upward past the avatar,
// giving a sense of downward speed (freefall / parachute descent).
// Works with any render pipeline — no special shaders needed.
public class WindEffect : MonoBehaviour
{
    [Tooltip("Avatar or canopy transform — wind particles spawn around this point")]
    public Transform followTarget;

    [Tooltip("Number of wind streak particles")]
    public int particleCount = 60;

    [Tooltip("Spawn radius around the avatar (metres)")]
    public float spawnRadius = 10f;

    [Tooltip("Upward drift speed relative to avatar (m/s) — simulates descent wind")]
    public float driftSpeed = 8f;

    [Tooltip("How long each particle lives before re-spawning (seconds)")]
    public float particleLifetime = 2f;

    struct Particle
    {
        public GameObject go;
        public Vector3    localOffset;   // relative to followTarget at spawn time
        public float      age;
        public float      maxAge;
    }

    Particle[] _particles;
    Material   _mat;

    void Start()
    {
        _mat = new Material(Shader.Find("Sprites/Default"));
        _mat.color = new Color(0.85f, 0.92f, 1f, 0.35f);

        _particles = new Particle[particleCount];
        for (int i = 0; i < particleCount; i++)
            _particles[i] = SpawnParticle(Random.Range(0f, particleLifetime));
    }

    void Update()
    {
        Vector3 center = followTarget != null ? followTarget.position : transform.position;
        float dt = Time.deltaTime;

        for (int i = 0; i < _particles.Length; i++)
        {
            ref Particle p = ref _particles[i];
            p.age += dt;

            // Drift opposite to direction of travel: up (descent) + backward past the face (glide)
            Vector3 fwd = (followTarget != null) ? followTarget.forward : Vector3.forward;
            Vector3 drift = (Vector3.up * 0.5f + (-fwd) * 1f).normalized * driftSpeed;
            p.localOffset += drift * dt;

            p.go.transform.position = center + p.localOffset;

            // Fade out near end of life
            float t = p.age / p.maxAge;
            float alpha = 0.35f * (1f - Mathf.Pow(t, 2f));
            _mat.color = new Color(0.85f, 0.92f, 1f, alpha);

            if (p.age >= p.maxAge)
            {
                Destroy(p.go);
                _particles[i] = SpawnParticle(0f);
            }
        }
    }

    Particle SpawnParticle(float startAge)
    {
        Vector3 center = followTarget != null ? followTarget.position : transform.position;

        // Random point on a sphere shell
        Vector3 offset = Random.onUnitSphere * spawnRadius;
        // Bias to spawn below and in front — particles then drift up and backward past the avatar
        Vector3 fwd = followTarget != null ? followTarget.forward : Vector3.forward;
        offset.y = -Mathf.Abs(offset.y);  // below
        if (Vector3.Dot(offset, fwd) < 0) offset -= 2f * Vector3.Project(offset, fwd); // flip to front hemisphere

        float scale = Random.Range(0.08f, 0.22f);
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "WindParticle";
        go.transform.SetParent(transform, true);
        go.transform.position = center + offset;
        go.transform.localScale = Vector3.one * scale;

        // Remove the collider — we only want a visual
        Destroy(go.GetComponent<Collider>());

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = _mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return new Particle
        {
            go          = go,
            localOffset = offset,
            age         = startAge,
            maxAge      = Random.Range(particleLifetime * 0.6f, particleLifetime * 1.4f),
        };
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
