using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Threading;

/// Receives the lab/Matlab physics stream over UDP and drives the parachute +
/// skydiver directly, replacing PlayerMovement's internal physics. Unity becomes
/// a pure renderer of the simulator's state.
///
/// Attach to any GameObject. Wire the canopy + body Rigidbodies and (optionally)
/// the PlayerMovement to disable. Then run the Matlab sender (Matlab/sim_to_unity.m)
/// or the real lab simulator on the same port.
///
/// ── WIRE PROTOCOL ────────────────────────────────────────────────────────────
/// One UDP datagram per frame: an ASCII line of 29 comma-separated numbers, in
/// this fixed order (matches Matlab/sim_to_unity.m):
///
///   0..2   canopy position    X,Y,Z
///   3..5   skydiver position  X,Y,Z      (center of gravity)
///   6..8   canopy orientation roll,pitch,yaw   (degrees)
///   9..11  skydiver orientation roll,pitch,yaw (degrees)
///   12..14 canopy linear velocity   Vx,Vy,Vz
///   15..17 skydiver linear velocity Vx,Vy,Vz
///   18..20 canopy angular velocity  wx,wy,wz   (deg/s)
///   21..23 skydiver angular velocity wx,wy,wz  (deg/s)
///   24..26 wind vector vx,vy,vz
///   27     right steering input (0..1)
///   28     left steering input  (0..1)
///
/// ── COORDINATE FRAMES ────────────────────────────────────────────────────────
/// FrameMode.EnuZupAerospace (default): the wire frame is right-handed, Z = up
/// (altitude), X = east, Y = north, meters; yaw about +Z (heading, CCW from east),
/// pitch about +Y, roll about +X. The receiver converts to Unity's left-handed
/// Y-up frame. Use FrameMode.RawUnity if the sender already emits Unity coords
/// (X=right, Y=up, Z=forward; orientation = Unity euler pitch/yaw/roll).
public class SimulatorReceiver : MonoBehaviour
{
    [Header("Network")]
    [Tooltip("UDP port to listen on. Must differ from the XSens stream (9763).")]
    public int listenPort = 9764;

    [Header("Driven objects")]
    [Tooltip("Canopy Rigidbody — driven to the simulator's canopy position + attitude.")]
    public Rigidbody canopy;

    [Tooltip("Skydiver body Rigidbody (the Avatar root that holds the Animator/XsensDevice). " +
             "The simulator's CG channel moves it through the world; XSens still poses the " +
             "limbs locally on top — the two don't fight.")]
    public Rigidbody body;

    [Tooltip("The scene's ProceduralCanopy. While the stream is live its avatar-follow is " +
             "switched off so the simulator is the sole authority over canopy position " +
             "(otherwise the follow yanks the canopy back over the avatar and the rig tears " +
             "apart). Auto-found if left empty.")]
    public ProceduralCanopy proceduralCanopy;

    [Tooltip("Also rotate the skydiver body to the simulator's body heading. Leave OFF if the " +
             "body's facing should come from the XSens mocap instead (recommended).")]
    public bool driveBodyRotation = false;

    [Tooltip("PlayerMovement to disable so its internal physics don't fight the stream.")]
    public PlayerMovement playerMovement;

    [Header("Coordinate frame")]
    public FrameMode frameMode = FrameMode.EnuZupAerospace;

    [Tooltip("Added to the canopy/body heading (deg). The canopy mesh faces Unity +Z at " +
             "rest, so sim heading 0 should also face +Z — leave at 0. Use this only to " +
             "align if the simulator's heading zero points elsewhere (e.g. 90/180).")]
    public float headingOffsetDeg = 0f;

    [Tooltip("Flip the sign of the simulator's heading if turns go the wrong way.")]
    public bool invertHeading = false;

    [Header("Stream gating")]
    [Tooltip("Until the first packet arrives, the canopy + body are frozen (kinematic, " +
             "zero velocity) so NOTHING moves or rotates without the simulator. Leave on.")]
    public bool freezeUntilStream = true;

    [Tooltip("If no packet arrives for this many seconds while live, re-freeze in place so " +
             "nothing drifts after the stream stops. Set 0 to never re-freeze.")]
    public float streamTimeout = 0.5f;

    [Header("Debug")]
    [Tooltip("Log the first received packet and connection status.")]
    public bool logPackets = true;

    public enum FrameMode { EnuZupAerospace, RawUnity }

    // Latest decoded packet, shared between the receive thread and the main thread.
    const int N = 29;
    readonly float[] _latest = new float[N];
    readonly object  _lock = new object();
    bool _hasNew;
    long _packetCount;

    // Stream state (main thread only).
    bool      _streamLive;        // true once we've taken over from a live packet
    float     _lastPacketTime;    // Time.time of the last applied packet
    Transform _followTarget;      // ProceduralCanopy's avatar-follow target, cached so we
                                  // can switch it off while live and restore it when frozen

    UdpClient _client;
    Thread    _thread;
    volatile bool _running;

    public static Vector3 Wind { get; private set; }

    void OnEnable()
    {
        StartReceiver();
    }

    void Start()
    {
        // Stop PlayerMovement's own physics — the stream is now authoritative.
        // Auto-find it if the slot wasn't wired, so its glide model can't spin
        // the canopy when no simulator is connected.
        if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null) playerMovement.enabled = false;

        // Cache the canopy's avatar-follow so we can disable it while the stream drives
        // both bodies, and restore it for the frozen no-stream preview.
        if (proceduralCanopy == null) proceduralCanopy = FindFirstObjectByType<ProceduralCanopy>();
        if (proceduralCanopy != null) _followTarget = proceduralCanopy.followTarget;

        // Nothing moves until the simulator says so: hard-freeze both bodies
        // (kinematic, zero velocity) until the first packet arrives.
        SetFrozen(freezeUntilStream);
    }

    void StartReceiver()
    {
        try
        {
            _client = new UdpClient(listenPort);
            _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true };
            _thread.Start();
            if (logPackets) Debug.Log($"[SimulatorReceiver] Listening on UDP {listenPort}.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SimulatorReceiver] Could not bind UDP {listenPort}: {e.Message}");
        }
    }

    void ReceiveLoop()
    {
        var any = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                byte[] data = _client.Receive(ref any);
                string text = System.Text.Encoding.ASCII.GetString(data);
                string[] parts = text.Split(',');
                if (parts.Length < N) continue;

                var tmp = new float[N];
                bool ok = true;
                for (int i = 0; i < N; i++)
                    if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out tmp[i]))
                    { ok = false; break; }
                if (!ok) continue;

                lock (_lock)
                {
                    Array.Copy(tmp, _latest, N);
                    _hasNew = true;
                    _packetCount++;
                }
            }
            catch (SocketException) { /* socket closed on shutdown */ }
            catch (Exception e) { Debug.LogWarning($"[SimulatorReceiver] {e.Message}"); }
        }
    }

    void FixedUpdate()
    {
        float[] f;
        bool gotPacket;
        lock (_lock)
        {
            gotPacket = _hasNew;
            f = gotPacket ? (float[])_latest.Clone() : null;
            _hasNew = false;
        }

        if (!gotPacket)
        {
            // No packet this frame. If the stream went quiet, re-freeze in place so
            // the canopy holds its last pose instead of drifting on stale velocity.
            if (_streamLive && freezeUntilStream && streamTimeout > 0f &&
                Time.time - _lastPacketTime > streamTimeout)
            {
                _streamLive = false;
                SetFrozen(true);
                if (logPackets) Debug.Log("[SimulatorReceiver] Stream stopped — frozen in place.");
            }
            return;   // frozen / waiting: nothing moves without the simulator
        }

        // A real packet arrived — take over physics if we were frozen.
        if (!_streamLive)
        {
            _streamLive = true;
            SetFrozen(false);
            if (logPackets) Debug.Log("[SimulatorReceiver] First packet received — stream is live.");
        }
        _lastPacketTime = Time.time;

        // Canopy: full pose from the simulator (pos@0 rot@6 linvel@12 angvel@18).
        // Position + attitude both come from the stream; the avatar-follow is off
        // (see SetFrozen) so nothing else competes for the canopy's position.
        ApplyCanopy(f);

        // Skydiver: the simulator's CG channel (pos@3) carries the Avatar root through
        // the world. XSens poses the limbs locally on top, so we move only position
        // (and heading if asked) — never the limbs.
        ApplySkydiver(f);

        Wind = ConvPos(f[24], f[25], f[26]);

        // Right=27, Left=28 → steering toggles (ToggleArmAnimation turns these into
        // shoulder pitch: 0 = arms up, 1 = arms down along the body).
        PlayerMovement.SetToggles(f[28], f[27]);
    }

    // Freeze (kinematic, zero velocity) or release the driven bodies, and toggle the
    // canopy's avatar-follow. While frozen nothing — not gravity, joints, nor leftover
    // velocity — can move them, and the canopy follows the avatar so the no-stream
    // preview looks right. While live the bodies are non-kinematic (so the canopy's
    // linearVelocity feeds the HUD) and the follow is OFF so the simulator alone
    // positions the canopy.
    void SetFrozen(bool frozen)
    {
        // The avatar body is always position-driven (never physics) — keep it kinematic
        // so gravity/collisions can never shove it; XSens still poses the limbs.
        if (body != null)
        {
            body.useGravity = false;
            if (!body.isKinematic)
            {
                body.linearVelocity  = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
        }

        // The canopy is kinematic while frozen (nothing can move it) and non-kinematic
        // while live so its linearVelocity feeds the HUD's SPD/HDG.
        if (canopy != null)
        {
            canopy.useGravity = false;
            if (frozen)
            {
                canopy.linearVelocity  = Vector3.zero;
                canopy.angularVelocity = Vector3.zero;
                canopy.isKinematic     = true;
            }
            else
            {
                canopy.isKinematic = false;
            }
        }

        // Restore the avatar-follow when frozen; switch it off when the stream drives.
        if (proceduralCanopy != null)
            proceduralCanopy.followTarget = frozen ? _followTarget : null;
    }

    void ApplyCanopy(float[] f)
    {
        if (canopy == null) return;
        canopy.position        = ConvPos(f[0],  f[1],  f[2]);
        canopy.rotation        = ConvRot(f[6],  f[7],  f[8]);
        canopy.linearVelocity  = ConvPos(f[12], f[13], f[14]);             // for HUD SPD/HDG
        canopy.angularVelocity = ConvPos(f[18], f[19], f[20]) * Mathf.Deg2Rad;
    }

    void ApplySkydiver(float[] f)
    {
        if (body == null) return;
        // Direct (teleport) placement — physics never touches the avatar; XSens poses
        // the limbs locally relative to this root.
        body.position = ConvPos(f[3], f[4], f[5]);
        if (driveBodyRotation)
            body.rotation = ConvRot(f[9], f[10], f[11]);
    }

    // ── frame conversion ────────────────────────────────────────────────────────

    Vector3 ConvPos(float x, float y, float z)
    {
        // ENU Z-up (east, north, up) → Unity (right=east, up, forward=north).
        return frameMode == FrameMode.RawUnity
            ? new Vector3(x, y, z)
            : new Vector3(x, z, y);
    }

    Quaternion ConvRot(float roll, float pitch, float yaw)
    {
        if (frameMode == FrameMode.RawUnity)
            return Quaternion.Euler(pitch, yaw, roll);   // already Unity euler (X,Y,Z)

        // Body-frame attitude → Unity. The canopy mesh is built Z=forward(chord),
        // X=span(right), Y=up, so: roll (bank) = rotation about forward/Z, pitch about
        // span/X, yaw (heading) about up/Y. The sim frame is right-handed and Unity is
        // left-handed, so each angle is negated; heading also gets a calibration offset.
        // (No fixed 90° offset — that wrongly assumed the canopy faced +X and was what
        // spun the rig sideways.)
        float uPitch = -pitch;
        float uRoll  = -roll;
        float uYaw   = headingOffsetDeg + (invertHeading ? yaw : -yaw);
        return Quaternion.Euler(uPitch, uYaw, uRoll);
    }

    // ── shutdown ────────────────────────────────────────────────────────────────

    void OnDisable()
    {
        _running = false;
        try { _client?.Close(); } catch { }
        _client = null;
        try { _thread?.Join(200); } catch { }
        _thread = null;
    }
}
