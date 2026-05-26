using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

// Attach to the Main Camera GameObject.
// In VR (Quest 2): disables CameraFollow and manually reads the HMD pose
// every LateUpdate via the XR InputDevices API. The camera's initial scene
// position becomes the "anchor" — the HMD's tracked offset is added to it,
// so you stay in the right part of the world while getting full 6DOF tracking.
// In desktop/editor mode: does nothing; CameraFollow works as normal.
//
// WHY manual pose reading instead of TrackedPoseDriver or XR Origin:
// Unity 6 removed the legacy auto-apply of XR pose to Main Camera.
// TrackedPoseDriver requires scene restructuring into an XR Origin hierarchy.
// Reading from InputDevices.GetDeviceAtXRNode(XRNode.Head) works with the
// existing bare-camera scene layout and needs no new packages.
public class VRCameraRig : MonoBehaviour
{
    [Tooltip("The CameraFollow component on this camera — will be disabled in VR mode")]
    public CameraFollow cameraFollow;

    [Tooltip("Canopy or Avatar transform — camera rides with this object in VR")]
    public Transform followTarget;

    [Tooltip("Offset from followTarget (e.g. eye level under canopy)")]
    public Vector3 followOffset = new Vector3(0f, -2f, 0f);

    private bool _vrMode = false;
    private InputDevice _hmd;
    private Vector3 _sceneAnchor;

    void Start()
    {
        _sceneAnchor = transform.position;
        StartCoroutine(InitXR());
    }

    IEnumerator InitXR()
    {
        float timeout = 5f;
        while (timeout > 0f)
        {
            _hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (_hmd.isValid) break;
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (!_hmd.isValid)
        {
            Debug.Log("[VRCameraRig] No HMD detected in 5s — desktop mode, CameraFollow unchanged.");
            yield break;
        }

        Debug.Log($"[VRCameraRig] HMD active: {_hmd.name}");

        if (cameraFollow != null)
            cameraFollow.enabled = false;

        SetFloorTrackingOrigin();
        _vrMode = true;
    }

    // LateUpdate so our pose write happens after any physics/animation that
    // could reposition the camera in the same frame.
    void LateUpdate()
    {
        if (!_vrMode) return;

        if (!_hmd.isValid)
            _hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);

        // Anchor follows the canopy/avatar so the player rides with the physics
        if (followTarget != null)
            _sceneAnchor = followTarget.position + followOffset;

        // Position: moving anchor + HMD's room-scale offset
        if (_hmd.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
            transform.position = _sceneAnchor + pos;

        // Rotation: pure HMD orientation (head look-around)
        if (_hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
            transform.rotation = rot;
    }

    void SetFloorTrackingOrigin()
    {
        var subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var s in subsystems)
        {
            if (s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor))
                Debug.Log("[VRCameraRig] Tracking origin: Floor.");
        }
    }
}
