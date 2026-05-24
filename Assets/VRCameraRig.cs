using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

// Attach to the Main Camera GameObject alongside CameraFollow.
// When running on Quest 2, this disables CameraFollow (the headset drives the camera)
// and sets the tracking origin to Floor so height is calibrated correctly.
// In desktop/editor mode it does nothing — CameraFollow works as normal.
public class VRCameraRig : MonoBehaviour
{
    [Tooltip("The CameraFollow component on this camera — will be disabled in VR mode")]
    public CameraFollow cameraFollow;

    void Start()
    {
        StartCoroutine(WaitForXR());
    }

    IEnumerator WaitForXR()
    {
        float timeout = 5f;
        while (!XRSettings.isDeviceActive && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (!XRSettings.isDeviceActive)
        {
            Debug.Log("[VRCameraRig] No XR device active after 5s — desktop mode, CameraFollow unchanged.");
            yield break;
        }

        Debug.Log($"[VRCameraRig] XR device active: {XRSettings.loadedDeviceName}");

        if (cameraFollow != null)
            cameraFollow.enabled = false;

        SetFloorTrackingOrigin();
    }

    void SetFloorTrackingOrigin()
    {
        var subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        foreach (var s in subsystems)
        {
            bool ok = s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            if (ok)
                Debug.Log("[VRCameraRig] Tracking origin set to Floor.");
        }
    }
}
