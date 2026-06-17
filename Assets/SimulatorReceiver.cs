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
    public Rigidbody canopy;
    public Rigidbody body;

    [Tooltip("PlayerMovement to disable so its internal physics don't fight the stream.")]
    public PlayerMovement playerMovement;

    [Header("Coordinate frame")]
    public FrameMode frameMode = FrameMode.EnuZupAerospace;

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
        if (playerMovement != null) playerMovement.enabled = false;

        // We position the bodies ourselves each frame; no gravity, no Unity forces.
        foreach (var rb in new[] { canopy, body })
        {
            if (rb == null) continue;
            rb.useGravity = false;
        }
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
        long count;
        lock (_lock)
        {
            if (!_hasNew) return;
            f = (float[])_latest.Clone();
            _hasNew = false;
            count = _packetCount;
        }

        if (logPackets && count == 1)
            Debug.Log("[SimulatorReceiver] First packet received — stream is live.");

        Apply(canopy, f, 0, 6, 12, 18);   // canopy:   pos@0  rot@6  linvel@12 angvel@18
        Apply(body,   f, 3, 9, 15, 21);   // skydiver: pos@3  rot@9  linvel@15 angvel@21

        Wind = ConvPos(f[24], f[25], f[26]);

        // Right=27, Left=28 → steering toggles (ToggleArmAnimation turns these into
        // shoulder pitch: 0 = arms up, 1 = arms down along the body).
        PlayerMovement.SetToggles(f[28], f[27]);
    }

    void Apply(Rigidbody rb, float[] f, int pos, int rot, int lin, int ang)
    {
        if (rb == null) return;
        rb.position        = ConvPos(f[pos],     f[pos + 1], f[pos + 2]);
        rb.rotation        = ConvRot(f[rot],     f[rot + 1], f[rot + 2]);
        rb.linearVelocity  = ConvPos(f[lin],     f[lin + 1], f[lin + 2]);   // for HUD SPD/HDG
        rb.angularVelocity = ConvPos(f[ang],     f[ang + 1], f[ang + 2]) * Mathf.Deg2Rad;
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

        // Aerospace ENU: yaw about up (CCW from east), pitch about east, roll about north.
        // Unity forward (+Z) = north, so heading 0 (east) faces Unity +X → Unity yaw = 90 - yaw.
        float uYaw   = 90f - yaw;
        float uPitch = -pitch;
        float uRoll  = -roll;
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
