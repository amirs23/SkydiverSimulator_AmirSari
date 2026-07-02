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
/// One UDP datagram per frame: an ASCII line of 26 comma-separated numbers, in
/// the EXACT field order of the lab simulator's `x_s` struct (see
/// simPhysicsOutput/Simulation Output.pdf and Matlab/sim_replay.m). `b` = canopy
/// (parachute body), `s` = skydiver/store:
///
///   0      t            time (s)            — unused by the renderer
///   1..3   x_i_b y_i_b z_i_b        canopy position   (m)
///   4..6   x_i_s y_i_s z_i_s        skydiver position (m)
///   7..9   Vx_b Vy_b Vz_b           canopy velocity   (m/s)
///   10..11 Vx_s Vy_s                skydiver velocity (m/s; sim has no Vz_s)
///   12..14 p_b q_b r_b              canopy body rates (rad/s)
///   15..17 p_s q_s r_s              skydiver body rates (rad/s)
///   18..20 phi_b theta_b psi_b      canopy attitude   roll,pitch,yaw (deg)
///   21..23 phi_s theta_s psi_s      skydiver attitude roll,pitch,yaw (deg)
///   24     delta_l                  left toggle  (normalized; see toggleScale)
///   25     delta_r                  right toggle (normalized; see toggleScale)
///
/// ── COORDINATE FRAMES ────────────────────────────────────────────────────────
/// FrameMode.EnuZupAerospace (default — name kept for back-compat): the simulator
/// frame is a standard right-handed GNC / NED frame — X = forward(north),
/// Y = right(east), Z = down — with 3-2-1 (yaw psi → pitch theta → roll phi) Euler
/// angles in degrees. The receiver converts attitude to Unity's left-handed Y-up
/// frame via a full axis-remapped LookRotation (NOT per-axis negation), which is
/// what fixes the canopy-orientation bug. NOTE: in the example `x_s` data the
/// position Z channel reads as ALTITUDE (up), counting down 1000→270 as it
/// descends, while the velocity Vz is down-positive — `positionZIsAltitude`
/// handles that mismatch. Use FrameMode.RawUnity if a sender already emits Unity
/// coords (X=right, Y=up, Z=forward; orientation = Unity euler pitch/yaw/roll).
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

    [Tooltip("The scene's ProceduralCanopy. The canopy POSITION always comes from its " +
             "avatar-follow (canopy = avatar + offset), so the ~7 m suspension rig stays " +
             "intact; the simulator drives only the canopy's ATTITUDE on top. (The sim " +
             "separates canopy and skydiver by ~2 m, far less than the visual rig, so we " +
             "never position the canopy absolutely from the sim.) Auto-found if left empty.")]
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

    [Tooltip("The example sim stores position Z as ALTITUDE (up): it counts down " +
             "1000→270 as the canopy descends. Leave ON so Unity height tracks it. " +
             "Turn OFF only if the lab confirms position Z is the down coordinate.")]
    public bool positionZIsAltitude = true;

    [Tooltip("Multiplies the raw delta_l/delta_r toggle channel before it drives the " +
             "arms. The sim normalizes the toggles (full pull here = 0.01), so 100 maps " +
             "0.01 → 1.0 (full pull). Tune to taste — left as a controllable knob per the spec.")]
    public float toggleScale = 100f;

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
    const int N = 26;   // 26 fields of the simulator's x_s struct (see WIRE PROTOCOL)

    // Field offsets into the packet (b = canopy, s = skydiver).
    // (canopy position@1..3 is intentionally unused — see ApplyCanopy.)
    const int S_POS = 4, B_VEL = 7, B_RATE = 12, B_ATT = 18, S_ATT = 21,
              DELTA_L = 24, DELTA_R = 25;

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

        // Canopy: ATTITUDE + velocity from the simulator. Its POSITION is left to the
        // avatar-follow (canopy = avatar + offset) so the suspension rig stays intact —
        // see ApplyCanopy / SetFrozen.
        ApplyCanopy(f);

        // Skydiver: the simulator's CG channel (pos@3) carries the Avatar root through
        // the world. XSens poses the limbs locally on top, so we move only position
        // (and heading if asked) — never the limbs.
        ApplySkydiver(f);

        // The simulator stream carries no wind channel — leave it zero so nothing
        // reads a stale gust. (Wind visuals can be driven separately if needed.)
        Wind = Vector3.zero;

        // delta_l@24, delta_r@25 → steering toggles, scaled out of the sim's
        // normalization (ToggleArmAnimation turns these into shoulder pitch:
        // 0 = arms up, 1 = arms down along the body).
        PlayerMovement.SetToggles(f[DELTA_L] * toggleScale, f[DELTA_R] * toggleScale);
    }

    // Freeze (kinematic, zero velocity) or release the driven bodies. While frozen
    // nothing — not gravity, joints, nor leftover velocity — can move them. While live
    // the canopy is non-kinematic so its linearVelocity feeds the HUD. The avatar-follow
    // stays ON in BOTH states: the canopy is always positioned at avatar + offset, so the
    // rig holds together whether or not the stream is running — the simulator only adds
    // the canopy's attitude on top.
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

        // Keep the avatar-follow ON in both states so the canopy always rides above the
        // avatar and the suspension rig stays intact (the sim drives only its attitude).
        if (proceduralCanopy != null)
            proceduralCanopy.followTarget = _followTarget;
    }

    void ApplyCanopy(float[] f)
    {
        if (canopy == null) return;
        // NOTE: canopy POSITION is intentionally NOT set here — ProceduralCanopy's
        // avatar-follow keeps it at avatar + offset every LateUpdate so the rig holds
        // together. We only drive attitude (banking turns) + velocity (HUD) on top.
        canopy.rotation        = ConvRot(f[B_ATT],  f[B_ATT + 1],  f[B_ATT + 2]);  // roll,pitch,yaw
        canopy.linearVelocity  = ConvVel(f[B_VEL],  f[B_VEL + 1],  f[B_VEL + 2]);   // for HUD SPD/HDG
        // Body rates p,q,r (rad/s) about forward/right/down → Unity ang. vel about
        // right(X)/up(Y)/forward(Z): (q, -r, p). Already radians — no Deg2Rad.
        canopy.angularVelocity = new Vector3(f[B_RATE + 1], -f[B_RATE + 2], f[B_RATE]);
    }

    void ApplySkydiver(float[] f)
    {
        if (body == null) return;
        // Direct (teleport) placement — physics never touches the avatar; XSens poses
        // the limbs locally relative to this root.
        body.position = ConvPos(f[S_POS], f[S_POS + 1], f[S_POS + 2]);
        if (driveBodyRotation)
            body.rotation = ConvRot(f[S_ATT], f[S_ATT + 1], f[S_ATT + 2]);
    }

    // ── frame conversion ────────────────────────────────────────────────────────

    // A direction in the simulator's NED frame (north, east, down) → Unity
    // (right = east, up = -down, forward = north). This single remap is what makes
    // both positions and attitudes come out right despite the handedness flip.
    static Vector3 NedDirToUnity(Vector3 ned) => new Vector3(ned.y, -ned.z, ned.x);

    // Absolute position. In the example data the Z column is altitude (up), so we feed
    // it in as down = -altitude; if the lab confirms Z is really the down coordinate,
    // turn positionZIsAltitude off and it's passed straight through.
    Vector3 ConvPos(float north, float east, float zCol)
    {
        if (frameMode == FrameMode.RawUnity) return new Vector3(north, east, zCol);
        float down = positionZIsAltitude ? -zCol : zCol;
        return NedDirToUnity(new Vector3(north, east, down));
    }

    // Linear velocity. Vz is genuinely down-positive in the sim (positive while
    // descending), so no altitude flip here — straight NED→Unity.
    Vector3 ConvVel(float vNorth, float vEast, float vDown)
    {
        if (frameMode == FrameMode.RawUnity) return new Vector3(vNorth, vEast, vDown);
        return NedDirToUnity(new Vector3(vNorth, vEast, vDown));
    }

    Quaternion ConvRot(float rollDeg, float pitchDeg, float yawDeg)
    {
        if (frameMode == FrameMode.RawUnity)
            return Quaternion.Euler(pitchDeg, yawDeg, rollDeg);   // already Unity euler (X,Y,Z)

        // NED 3-2-1 (yaw psi → pitch theta → roll phi) attitude → Unity. Instead of
        // negating each Euler angle (which assumed the wrong frame and was the cause of
        // the bad canopy orientation), build the body's forward and up axes in NED from
        // the rotation matrix R = Rz(psi)·Ry(theta)·Rx(phi), remap each axis into Unity,
        // and reconstruct the rotation with LookRotation. This is exact across the
        // right-handed(NED) → left-handed(Unity) handedness change.
        float yaw   = invertHeading ? -yawDeg : yawDeg;
        float phi   = rollDeg  * Mathf.Deg2Rad;   // roll  about forward/X
        float theta = pitchDeg * Mathf.Deg2Rad;   // pitch about right/Y
        float psi   = yaw      * Mathf.Deg2Rad;   // yaw   about down/Z

        float cphi = Mathf.Cos(phi),   sphi = Mathf.Sin(phi);
        float cth  = Mathf.Cos(theta), sth  = Mathf.Sin(theta);
        float cpsi = Mathf.Cos(psi),   spsi = Mathf.Sin(psi);

        // Columns of R (body axes expressed in NED).
        Vector3 fwdNed  = new Vector3(cth * cpsi, cth * spsi, -sth);                 // body +X (forward)
        Vector3 downNed = new Vector3(cphi * sth * cpsi + sphi * spsi,               // body +Z (down)
                                      cphi * sth * spsi - sphi * cpsi,
                                      cphi * cth);

        Vector3 fwdU = NedDirToUnity(fwdNed);
        Vector3 upU  = NedDirToUnity(-downNed);
        Quaternion q = Quaternion.LookRotation(fwdU, upU);

        // Optional manual heading trim (applied in Unity's world-up).
        if (headingOffsetDeg != 0f) q = Quaternion.Euler(0f, headingOffsetDeg, 0f) * q;
        return q;
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
