using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Animates the avatar's arms to match toggle inputs.
///
/// In neutral (no pull): arms are raised up, hands reaching toward the toggles.
/// When pulling left toggle: left arm pulls down toward hip.
/// When pulling right toggle: right arm pulls down toward hip.
/// Both pulled: full brake — both arms down.
///
/// Setup:
///   1. Add this component to the Avatar GameObject (or any GameObject in the scene).
///   2. Drag the arm bones from the Avatar skeleton into the Inspector slots.
///   3. Adjust Pull Euler and Raised Euler in the Inspector until the motion looks right
///      — exact values depend on how the avatar skeleton is oriented.
///
/// Keyboard (Editor testing):  A = left toggle,  D = right toggle,  Space = both.
/// Quest: left trigger = left toggle, right trigger = right toggle.
/// </summary>
public class ToggleArmAnimation : MonoBehaviour
{
    [Header("Left Arm Bones")]
    [Tooltip("Upper arm bone (shoulder joint). e.g. jLeftArm")]
    public Transform leftUpperArm;
    [Tooltip("Forearm bone (elbow joint). e.g. jLeftForeArm")]
    public Transform leftForeArm;

    [Header("Right Arm Bones")]
    [Tooltip("Upper arm bone (shoulder joint). e.g. jRightArm")]
    public Transform rightUpperArm;
    [Tooltip("Forearm bone (elbow joint). e.g. jRightForeArm")]
    public Transform rightForeArm;

    [Header("Raised Position (neutral — hands up at toggles)")]
    [Tooltip("LOCAL Euler offset added to the recorded T-pose when arms are fully raised. " +
             "Tune X/Y/Z until arms point upward to the toggles at rest.")]
    public Vector3 raisedUpperEuler = new Vector3(-60f, 0f, 0f);
    public Vector3 raisedForeEuler  = new Vector3(-30f, 0f, 0f);

    [Header("Pulled Position (full toggle pull — hands down at hip)")]
    [Tooltip("LOCAL Euler offset when toggle is fully pulled. " +
             "Tune until arm swings down and inward realistically.")]
    public Vector3 pulledUpperEuler = new Vector3(40f, 0f, 0f);
    public Vector3 pulledForeEuler  = new Vector3(60f, 0f, 0f);

    [Header("Smoothing")]
    [Tooltip("How quickly the arms follow the toggle input (higher = snappier).")]
    public float smoothSpeed = 10f;

    // Cached T-pose local rotations (recorded at Start before any animation)
    Quaternion _leftUpperTPose,  _leftForeTPose;
    Quaternion _rightUpperTPose, _rightForeTPose;

    // Smoothed toggle values
    float _leftSmooth, _rightSmooth;

    static UnityEngine.XR.InputDevice s_rightCtrl;
    static UnityEngine.XR.InputDevice s_leftCtrl;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        if (leftUpperArm)  _leftUpperTPose  = leftUpperArm.localRotation;
        if (leftForeArm)   _leftForeTPose   = leftForeArm.localRotation;
        if (rightUpperArm) _rightUpperTPose = rightUpperArm.localRotation;
        if (rightForeArm)  _rightForeTPose  = rightForeArm.localRotation;
    }

    void LateUpdate()
    {
        // ── Read toggle inputs ────────────────────────────────────────────────
        // Try PlayerMovement static values first (set by the physics update).
        // Fall back to reading hardware directly so this works even without PlayerMovement.
        float leftRaw  = PlayerMovement.LeftToggle;
        float rightRaw = PlayerMovement.RightToggle;

        // Direct hardware fallback (also handles keyboard in Editor)
        if (!s_leftCtrl.isValid)
            s_leftCtrl = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        if (!s_rightCtrl.isValid)
            s_rightCtrl = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);

        s_leftCtrl.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger,  out float lHW);
        s_rightCtrl.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float rHW);
        leftRaw  = Mathf.Max(leftRaw,  lHW);
        rightRaw = Mathf.Max(rightRaw, rHW);

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed)     leftRaw  = 1f;
            if (Keyboard.current.dKey.isPressed)     rightRaw = 1f;
            if (Keyboard.current.spaceKey.isPressed) { leftRaw = 1f; rightRaw = 1f; }
        }

        // ── Smooth ────────────────────────────────────────────────────────────
        float dt = Time.deltaTime;
        _leftSmooth  = Mathf.Lerp(_leftSmooth,  leftRaw,  dt * smoothSpeed);
        _rightSmooth = Mathf.Lerp(_rightSmooth, rightRaw, dt * smoothSpeed);

        // ── Apply to bones ────────────────────────────────────────────────────
        ApplyArm(leftUpperArm,  _leftUpperTPose,  raisedUpperEuler, pulledUpperEuler, _leftSmooth);
        ApplyArm(leftForeArm,   _leftForeTPose,   raisedForeEuler,  pulledForeEuler,  _leftSmooth);
        ApplyArm(rightUpperArm, _rightUpperTPose, raisedUpperEuler, pulledUpperEuler, _rightSmooth);
        ApplyArm(rightForeArm,  _rightForeTPose,  raisedForeEuler,  pulledForeEuler,  _rightSmooth);
    }

    // Lerp between the raised and pulled local-rotation offsets,
    // applied on top of the T-pose rotation.
    static void ApplyArm(Transform bone, Quaternion tPose,
                         Vector3 raisedEuler, Vector3 pulledEuler, float t)
    {
        if (bone == null) return;
        Vector3 euler = Vector3.Lerp(raisedEuler, pulledEuler, t);
        bone.localRotation = tPose * Quaternion.Euler(euler);
    }
}
