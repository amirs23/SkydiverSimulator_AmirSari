using UnityEngine;
using UnityEngine.InputSystem;

// Attach to the Main Camera (the same object that has VRCameraRig).
//
// Runtime switch between first/third person WHILE KEEPING HEAD TRACKING in both modes.
//
// Key idea: VRCameraRig is the only thing that gives head-tracked look-around
// (it writes pure HMD rotation every LateUpdate, and positions the camera at
// followTarget + followOffset + HMD offset). So instead of swapping to a
// LookAt-style chase cam (which would FIGHT your head rotation and lock the view
// onto the avatar), we keep VRCameraRig active and just change its followOffset:
//
//   - First person : offset at eye level         -> you see what the avatar sees
//   - Third person : offset pulled back + up      -> camera floats behind the
//                    avatar, far enough to see the whole canopy + pilot chute,
//                    and you can still turn your head to look around the world.
//
// Toggle three ways:
//   1. Keyboard V        (Editor / desktop)
//   2. Quest B button    (right controller, secondary — A is taken by restart)
//   3. Code: CameraViewController.Instance.SetView(thirdPerson) — e.g. a Matlab UDP flag
public class CameraViewController : MonoBehaviour
{
    [Header("Rig")]
    [Tooltip("The head-tracked rig on this Main Camera. Stays active in both views.")]
    public VRCameraRig vrCameraRig;

    [Tooltip("Optional: the chase cam. Disabled automatically so the desktop " +
             "preview below can drive the camera. Leave empty if not present.")]
    public CameraFollow cameraFollow;

    [Header("First person (eye level)")]
    [Tooltip("Drag a head/eye bone here. First person rides this bone directly. " +
             "Leave empty to fall back to the rig's followTarget.")]
    public Transform firstPersonBone;

    [Tooltip("Fine offset from the first-person bone (e.g. nudge forward to the eyes).")]
    public Vector3 firstPersonOffset = new Vector3(0f, 0f, 0f);

    [Tooltip("Desktop-only facing correction. XSens head bones don't always point " +
             "'forward' — if the editor preview looks up/down/sideways, tweak these " +
             "euler angles (try 90 or -90 on X). Ignored in VR (HMD drives rotation).")]
    public Vector3 firstPersonEuler = new Vector3(0f, 0f, 0f);

    [Header("Third person (behind, framing the full canopy)")]
    [Tooltip("Offset from followTarget when floating behind the avatar. " +
             "Pull Z negative (behind) and Y up; tune until the whole parachute + pilot chute fit.")]
    public Vector3 thirdPersonOffset = new Vector3(0f, 2f, -14f);

    [Header("State")]
    [Tooltip("Mode at launch. First person is the default in a headset.")]
    public bool thirdPerson = false;

    public static CameraViewController Instance { get; private set; }

    private bool _bWasDown;
    private Transform _thirdPersonTarget;   // the avatar/canopy the rig was originally following

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Remember what the rig follows in third person (the canopy/avatar wired on VRCameraRig).
        if (vrCameraRig != null)
            _thirdPersonTarget = vrCameraRig.followTarget;
        ApplyView();
    }

    void Update()
    {
        bool toggle = false;

        // Keyboard V (Editor / desktop)
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            toggle = true;

        // Quest: B button on right controller (edge detect)
        var rightCtrl = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        if (rightCtrl.isValid)
        {
            rightCtrl.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool bDown);
            if (bDown && !_bWasDown) toggle = true;
            _bWasDown = bDown;
        }

        if (toggle) ToggleView();
    }

    public void ToggleView() => SetView(!thirdPerson);

    public void SetView(bool useThirdPerson)
    {
        thirdPerson = useThirdPerson;
        ApplyView();
        Debug.Log($"[CameraView] {(thirdPerson ? "Third" : "First")} person");
    }

    void ApplyView()
    {
        // Same head-tracked rig in both modes. Swap WHAT it follows and the offset:
        //   third person -> the avatar/canopy, pulled back
        //   first person -> the head bone (if set), at eye level
        if (vrCameraRig != null)
        {
            if (thirdPerson)
            {
                vrCameraRig.followTarget = _thirdPersonTarget;
                vrCameraRig.followOffset = thirdPersonOffset;
            }
            else
            {
                vrCameraRig.followTarget = firstPersonBone != null ? firstPersonBone : _thirdPersonTarget;
                vrCameraRig.followOffset = firstPersonOffset;
            }
        }
    }

    // Desktop / Editor preview (no headset). When there's no HMD, VRCameraRig is
    // dormant, so we drive the camera here using the same followTarget + offset.
    // This makes V visibly switch first/third person without the VR headset on.
    void LateUpdate()
    {
        if (vrCameraRig == null) return;

        var hmd = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
        if (hmd.isValid) return; // VR mode — let VRCameraRig own the transform

        // No HMD: take over (and make sure the chase cam isn't fighting us).
        if (cameraFollow != null && cameraFollow.enabled)
            cameraFollow.enabled = false;

        var target = vrCameraRig.followTarget;
        if (target == null) return;

        Vector3 offset = thirdPerson ? thirdPersonOffset : firstPersonOffset;
        transform.position = target.position + offset;

        // Third person: look at the avatar/canopy so it's in frame.
        // First person: face the same way the target faces.
        if (thirdPerson)
            transform.LookAt(target.position + Vector3.up * 1f);
        else
            transform.rotation = target.rotation * Quaternion.Euler(firstPersonEuler);
    }
}
