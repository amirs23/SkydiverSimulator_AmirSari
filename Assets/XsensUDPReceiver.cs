using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receives XSens MVN pose over UDP (MXTP02) with a plain socket and drives the
/// avatar's bones directly — no Live Capture, no Editor connection.
///
/// WHY THIS EXISTS
/// ---------------
/// The `com.movella.xsens` plugin CANNOT work in a standalone build. Its data
/// arrives through a Live Capture *Connection*, and in
/// `com.unity.live-capture/Runtime/Core/Communication/ConnectionManager.cs` the only
/// call that loads saved connections —
///     InternalEditorUtility.LoadSerializedFileAndForget(k_AssetPath)
/// — is wrapped in `#if UNITY_EDITOR`. In a player build it is compiled out and the
/// manager is constructed EMPTY, so nothing ever binds port 9763. The connection is
/// also persisted to "UserSettings/LiveCapture/ConnectionManager.asset", a per-user
/// folder that is never shipped in a build. Hence: works in the Editor, dead in the
/// APK. That is by design — Live Capture drives the Editor, not players.
///
/// This receiver is the build-side replacement. It is a port of the AR project's
/// `XsensUDPReceiver.cs`, which was verified animating on-device (2026-05-26).
///
/// TWO THINGS DIFFER FROM THE AR VERSION — do not "restore" them:
///   1. ROTATION ONLY. In VR the simulator owns the avatar's world position
///      (SimulatorReceiver: "the simulator's CG channel carries the Avatar root
///      through the world. XSens poses the limbs locally on top"). AR's Hips-position
///      block would fight it every frame, so it is off unless you opt in.
///   2. BONE RESOLUTION. This avatar imports TWO copies of each bone: the real
///      skinned ones nested under Hips and suffixed " 1" by Unity (e.g.
///      "LeftCarpus 1"), and flat non-moving reference nodes at the rig root
///      ("LeftCarpus", "jLeftWrist") that sit at the torso. A plain name lookup binds
///      the WRONG ones and nothing visibly moves. Same trap ProceduralCanopy
///      documents; the resolver below mirrors its FindSkinnedBone().
///
/// SETUP: add to any GameObject, drag the Avatar root into `avatarRoot`, leave Mode
/// on BuildsOnly. Run `animate_to_unity.m` pointed at the headset's IP.
/// </summary>
public class XsensUDPReceiver : MonoBehaviour
{
    public enum ActiveMode
    {
        BuildsOnly,   // socket in players, plugin in the Editor — the normal setup
        Always,       // also run in the Editor (disable XsensDevice first, or they fight)
        Disabled,
    }

    [Header("Mode")]
    [Tooltip("BuildsOnly: the Live Capture plugin handles the Editor and this handles " +
             "the APK. Both drive the same bones, so never run both at once.")]
    public ActiveMode mode = ActiveMode.BuildsOnly;

    [Header("Network")]
    [Tooltip("MXTP02 port. Must match animate_to_unity.m. The simulator uses 9764.")]
    public int port = 9763;

    [Header("Avatar")]
    [Tooltip("Drag the Avatar root here (the same object ProceduralCanopy follows).")]
    public Transform avatarRoot;

    [Header("Position (leave OFF for VR)")]
    [Tooltip("Apply the XSens Hips height. OFF by default: SimulatorReceiver already " +
             "carries the avatar through the world, and both writing it fights.")]
    public bool applyHipsHeight = false;

    [Header("Debug")]
    [Tooltip("Log the first packet and the bone-cache result. Use with `adb logcat -s Unity` " +
             "to prove on-device whether anything reaches 9763 at all.")]
    public bool logPackets = true;

    // XSens segment IDs are 1-indexed. Maps segID → bone name in Avatar.FBX.
    static readonly string[] k_BoneNames = new string[24]
    {
        null,             // 0  unused (IDs start at 1)
        "Hips",           // 1  Pelvis
        "Chest",          // 2  L5
        "Chest2",         // 3  L3
        "Chest3",         // 4  T12
        "Chest4",         // 5  T8
        "Neck",           // 6  Neck
        "Head",           // 7  Head
        "RightCollar",    // 8  RightShoulder
        "RightShoulder",  // 9  RightUpperArm
        "RightElbow",     // 10 RightLowerArm
        "RightCarpus",    // 11 RightHand
        "LeftCollar",     // 12 LeftShoulder
        "LeftShoulder",   // 13 LeftUpperArm
        "LeftElbow",      // 14 LeftLowerArm
        "LeftCarpus",     // 15 LeftHand
        "RightHip",       // 16 RightUpperLeg
        "RightKnee",      // 17 RightLowerLeg
        "RightAnkle",     // 18 RightFoot
        "RightToe",       // 19 RightToe
        "LeftHip",        // 20 LeftUpperLeg
        "LeftKnee",       // 21 LeftLowerLeg
        "LeftAnkle",      // 22 LeftFoot
        "LeftToe",        // 23 LeftToe
    };

    struct SegmentPose
    {
        public Vector3    position;
        public Quaternion rotation;
    }

    readonly Transform[]  _bones     = new Transform[24];
    readonly Quaternion[] _tPoseRots = new Quaternion[24];
    Transform             _hips;
    bool                  _bonesReady;

    readonly object _lock        = new object();
    SegmentPose[]   _writeBuffer = new SegmentPose[23];
    SegmentPose[]   _readBuffer  = new SegmentPose[23];
    bool            _hasNewData;
    bool            _loggedFirst;

    UdpClient     _udp;
    Thread        _thread;
    volatile bool _running;

    bool ShouldRun()
    {
        switch (mode)
        {
            case ActiveMode.Disabled: return false;
            case ActiveMode.Always:   return true;
            default:                  return !Application.isEditor;
        }
    }

    // =========================================================================
    void Start()
    {
        if (!ShouldRun())
        {
            if (logPackets)
                Debug.Log("[XsensUDP] Idle — mode is BuildsOnly and this is the Editor. " +
                          "The Live Capture plugin drives the avatar here.");
            enabled = false;
            return;
        }

        CacheBones();
        StartListening();
    }

    // =========================================================================
    // BONE RESOLUTION — mirrors ProceduralCanopy.FindSkinnedBone().
    // Must prefer the Hips-rooted " 1" copies; the flat ones never move.
    // =========================================================================
    void CacheBones()
    {
        if (avatarRoot == null)
        {
            Debug.LogError("[XsensUDP] 'Avatar Root' not assigned — nothing to drive.");
            return;
        }

        int found = 0;
        for (int id = 1; id <= 23; id++)
        {
            var wanted = k_BoneNames[id];
            if (wanted == null) continue;

            var bone = FindSkinnedBone(wanted);
            if (bone != null)
            {
                _bones[id]     = bone;
                _tPoseRots[id] = bone.rotation;   // rest pose, captured before any streaming
                found++;
                if (id == 1) _hips = bone;
            }
            else if (logPackets)
            {
                Debug.LogWarning($"[XsensUDP] Bone not found: '{wanted}' (segID {id})");
            }
        }

        _bonesReady = found > 0 && _hips != null;

        if (!_bonesReady)
            Debug.LogError("[XsensUDP] Bone cache failed — found " + found +
                           "/23, hips=" + (_hips != null) + ". Check 'Avatar Root'.");
        else if (logPackets)
            Debug.Log($"[XsensUDP] Cached {found}/23 bones. Listening next.");
    }

    Transform FindSkinnedBone(string wanted)
    {
        Transform anyMatch = null;
        foreach (var t in avatarRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!NameMatches(t.name, wanted)) continue;
            if (wanted == "Hips" || HasAncestorNamed(t, "Hips")) return t;
            anyMatch ??= t;                       // fallback: a detached reference node
        }
        return anyMatch;
    }

    // "LeftCarpus 1" matches "LeftCarpus" — Unity appends " N" to de-duplicate names.
    static bool NameMatches(string actual, string wanted)
    {
        if (actual == wanted) return true;
        if (actual.Length > wanted.Length && actual.StartsWith(wanted) && actual[wanted.Length] == ' ')
        {
            for (int i = wanted.Length + 1; i < actual.Length; i++)
                if (!char.IsDigit(actual[i])) return false;
            return true;
        }
        return false;
    }

    static bool HasAncestorNamed(Transform t, string name)
    {
        for (Transform p = t.parent; p != null; p = p.parent)
            if (NameMatches(p.name, name)) return true;
        return false;
    }

    // =========================================================================
    // NETWORK
    // =========================================================================
    void StartListening()
    {
        try
        {
            _udp     = new UdpClient(port);
            _running = true;
            _thread  = new Thread(ReceiveLoop) { IsBackground = true, Name = "XsensUDP" };
            _thread.Start();
            Debug.Log($"[XsensUDP] Listening on UDP {port}.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[XsensUDP] Could not open UDP {port}: {e.Message} " +
                           "(port already bound? the Live Capture plugin may hold it in the Editor)");
        }
    }

    void ReceiveLoop()
    {
        var endpoint = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                byte[] data = _udp.Receive(ref endpoint);
                ParsePacket(data);
            }
            catch (SocketException) { break; }
            catch (Exception e)     { Debug.LogWarning($"[XsensUDP] {e.Message}"); }
        }
    }

    void ParsePacket(byte[] data)
    {
        if (data.Length < 25) return;
        if (data[0] != 'M' || data[1] != 'X' || data[2] != 'T' ||
            data[3] != 'P' || data[4] != '0' || data[5] != '2') return;

        int segCount = data[11];
        if (segCount < 1 || segCount > 23) return;

        var buf    = _writeBuffer;
        int offset = 24;

        for (int i = 0; i < segCount; i++)
        {
            if (offset + 32 > data.Length) break;

            uint  segID = ReadUInt32BE(data, offset); offset += 4;
            float px    = ReadFloatBE(data, offset);  offset += 4;
            float py    = ReadFloatBE(data, offset);  offset += 4;
            float pz    = ReadFloatBE(data, offset);  offset += 4;
            float qw    = ReadFloatBE(data, offset);  offset += 4;
            float qx    = ReadFloatBE(data, offset);  offset += 4;
            float qy    = ReadFloatBE(data, offset);  offset += 4;
            float qz    = ReadFloatBE(data, offset);  offset += 4;

            if (segID < 1 || segID > 23) continue;

            // MVN → Unity. Same conversion the AR build was verified with — if the
            // pose ever comes out mirrored or rolled, this line is the suspect.
            buf[segID - 1] = new SegmentPose
            {
                position = new Vector3(-py, pz, px),
                rotation = new Quaternion(qy, -qz, -qx, qw),
            };
        }

        lock (_lock)
        {
            (_writeBuffer, _readBuffer) = (_readBuffer, buf);
            _hasNewData = true;
        }

        if (logPackets && !_loggedFirst)
        {
            _loggedFirst = true;
            Debug.Log($"[XsensUDP] First packet received from {endpointDesc()} — {segCount} segments.");
        }
    }

    string endpointDesc() => "MXTP02 stream";

    // =========================================================================
    // APPLY  — runs before ProceduralCanopy ([DefaultExecutionOrder(10000)]), so the
    // suspension cables rebuild against the posed skeleton, not last frame's.
    // =========================================================================
    void LateUpdate()
    {
        if (!_bonesReady) return;

        SegmentPose[] poses;
        lock (_lock)
        {
            if (!_hasNewData) return;
            poses       = _readBuffer;
            _hasNewData = false;
        }

        var actorRot = _hips.parent ? _hips.parent.rotation : Quaternion.identity;
        var invActor = Quaternion.Inverse(actorRot);

        for (int id = 1; id <= 23; id++)
        {
            var bone = _bones[id];
            if (bone == null) continue;

            var pose      = poses[id - 1];
            var parentRot = bone.parent ? bone.parent.rotation : Quaternion.identity;
            var invParent = Quaternion.Inverse(invActor * parentRot);

            bone.localRotation = invParent * (pose.rotation * _tPoseRots[id]);
        }

        // OFF by default — SimulatorReceiver owns the avatar's world position in VR.
        if (applyHipsHeight && _hips != null)
        {
            float scale = _hips.lossyScale.y > 0f ? _hips.lossyScale.y : 1f;
            var   p     = _hips.position;
            _hips.position = new Vector3(p.x, poses[0].position.y / scale, p.z);
        }
    }

    void OnDestroy()
    {
        _running = false;
        _udp?.Close();
        _thread?.Join(500);
    }

    static uint ReadUInt32BE(byte[] b, int o) =>
        (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);

    static float ReadFloatBE(byte[] b, int o) =>
        BitConverter.ToSingle(new byte[] { b[o + 3], b[o + 2], b[o + 1], b[o] }, 0);
}
