using UnityEngine;
using UnityEngine.InputSystem;
using System.Runtime.InteropServices;

// Attach to any GameObject in the scene.
// Drag the canopy and body Rigidbodies into the Inspector slots.
// On Windows: calls EOM_Solver.dll (Matlab-compiled) every physics frame.
// On Android/Quest: runs a simplified C# parachute model — gravity, drag,
// glide, and toggle turns driven by Quest trigger buttons.
public class PlayerMovement : MonoBehaviour
{
    public Rigidbody rbCanopy;
    public Rigidbody rbBody;
    public Transform playerTransform;

    // Read by SkydiverHUD to show brake level on the HUD (0=no brake, 1=full flare).
    public static float BrakeLevel { get; private set; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("EOM_Solver")]
    private static extern void EOM_Solver_Native(
        double tCurrent,
        double[] xCurrentUnity_data,
        int[] xCurrentUnity_size,
        double dt,
        double[] xNextUnity_data,
        int[] xNextUnity_size
    );

    private static void CallEOM(double t, double[] xCur, int[] xCurSize, double dt, double[] xNext, int[] xNextSize)
        => EOM_Solver_Native(t, xCur, xCurSize, dt, xNext, xNextSize);
#else
    // -----------------------------------------------------------------------
    // Simplified parachute aerodynamics for Android (Quest 2).
    // EOM_Solver.dll is Windows x86-64 only — this replaces it with a pure C#
    // model so the HUD and physics-driven objects are animated on device.
    //
    // State vector layout (24 doubles, same as EOM_Solver contract):
    //   [0-2]  CanopyPos    [3-5]  CanopyRot(Euler) [6-8]  CanopyLinVel
    //   [9-11] CanopyAngVel [12-14]BodyPos          [15-17]BodyRot(Euler)
    //   [18-20]BodyLinVel   [21-23]BodyAngVel
    // -----------------------------------------------------------------------
    private const float TerminalVY  = -5f;      // m/s descent at terminal velocity
    private const float GlideRatio  = 2.5f;     // horizontal / vertical speed
    private const float KDrag       = 9.81f / 5f; // drag coefficient
    private const float TurnRateDeg = 30f;      // deg/s per unit of net toggle (−1..+1)
    private const float BodyOffsetY = -5f;      // body hangs 5 m below canopy

    private static UnityEngine.XR.InputDevice s_rightCtrl;
    private static UnityEngine.XR.InputDevice s_leftCtrl;

    private static float GetTrigger(UnityEngine.XR.XRNode node, ref UnityEngine.XR.InputDevice dev)
    {
        if (!dev.isValid)
            dev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
        dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float v);
        return v;
    }

    private static void CallEOM(double t, double[] xCur, int[] xCurSize, double dt, double[] xNext, int[] xNextSize)
    {
        System.Array.Copy(xCur, xNext, xCur.Length);

        float dT = (float)dt;

        // --- Read inputs ---
        float rightToggle = GetTrigger(UnityEngine.XR.XRNode.RightHand, ref s_rightCtrl);
        float leftToggle  = GetTrigger(UnityEngine.XR.XRNode.LeftHand,  ref s_leftCtrl);
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed) rightToggle = Mathf.Max(rightToggle, 1f);
            if (Keyboard.current.aKey.isPressed) leftToggle  = Mathf.Max(leftToggle,  1f);
            // Space = symmetric brake / flare (keyboard testing)
            if (Keyboard.current.spaceKey.isPressed)
            {
                rightToggle = Mathf.Max(rightToggle, 1f);
                leftToggle  = Mathf.Max(leftToggle,  1f);
            }
        }

        // Brake level = symmetric portion of both toggles (0 = no brake, 1 = full flare).
        // Differential portion still drives turning.
        float brakeLevel = Mathf.Min(rightToggle, leftToggle);
        BrakeLevel = brakeLevel;

        // Remove the symmetric brake component before computing turning.
        float rightTurn = rightToggle - brakeLevel;
        float leftTurn  = leftToggle  - brakeLevel;

        // --- Unpack canopy state ---
        float px = (float)xNext[0], py = (float)xNext[1], pz = (float)xNext[2];
        float ry = (float)xNext[4];   // yaw (heading), degrees
        float vx = (float)xNext[6], vy = (float)xNext[7], vz = (float)xNext[8];

        // --- Heading update from toggle differential ---
        float netToggle = rightTurn - leftTurn;   // negative = turn left
        ry += netToggle * TurnRateDeg * dT;
        ry  = ((ry % 360f) + 360f) % 360f;

        // --- Brake / flare effect on descent and glide ---
        // Full brake (brakeLevel=1): descent rate drops to ~0.3 m/s, forward speed ~0.3× normal.
        float effectiveTerminalVY = Mathf.Lerp(TerminalVY, -0.3f, brakeLevel);
        float effectiveGlide      = Mathf.Lerp(GlideRatio,  0.3f, brakeLevel);

        // --- Target velocity: glide forward + descend ---
        float headRad = ry * Mathf.Deg2Rad;
        float fwd     = -effectiveTerminalVY * effectiveGlide;    // always positive
        float desVx   = Mathf.Sin(headRad) * fwd;
        float desVy   = effectiveTerminalVY;
        float desVz   = Mathf.Cos(headRad) * fwd;

        // Exponential velocity relaxation toward target (simulates drag)
        float alpha = 1f - Mathf.Exp(-KDrag * dT);
        vx = Mathf.Lerp(vx, desVx, alpha);
        vy = Mathf.Lerp(vy, desVy, alpha);
        vz = Mathf.Lerp(vz, desVz, alpha);

        // --- Integrate position ---
        px += vx * dT;
        py += vy * dT;
        pz += vz * dT;

        // --- Write canopy back ---
        xNext[0] = px; xNext[1] = py;  xNext[2] = pz;
        // xNext[3] and xNext[5] (pitch / roll) unchanged
        xNext[4] = ry;
        xNext[6] = vx; xNext[7] = vy;  xNext[8] = vz;
        xNext[9] = 0;  xNext[10] = 0;  xNext[11] = 0;

        // --- Body: rigid attachment 8 m below canopy ---
        xNext[12] = px; xNext[13] = py + BodyOffsetY; xNext[14] = pz;
        // copy canopy rotation to body
        xNext[15] = xNext[3]; xNext[16] = ry; xNext[17] = xNext[5];
        xNext[18] = vx; xNext[19] = vy; xNext[20] = vz;
        xNext[21] = 0;  xNext[22] = 0;  xNext[23] = 0;

        xNextSize[0] = 24;
    }
#endif

    private State currentState = new State();
    private State nextState    = new State();
    private bool  _landed      = false;
    private bool  _aWasDown    = false;
    private Vector3 _initCanopyPos;
    private Vector3 _initBodyPos;

    void Start()
    {
        if (rbCanopy != null)
        {
            rbCanopy.useGravity = false;
            if (rbCanopy.position.y < 10f)
                rbCanopy.position = new Vector3(rbCanopy.position.x, 75f, rbCanopy.position.z);
        }
        if (rbBody != null)
        {
            rbBody.useGravity = false;
            if (rbBody.position.y < 10f)
                rbBody.position = new Vector3(rbBody.position.x, 70f, rbBody.position.z);
        }

        // Auto-position landing zone using actual canopy heading.
        // Body hits ground when canopy.y = 5 (body is 5m below canopy),
        // so effective descent = start.y - 5. Forward dist = GlideRatio * effectiveHeight.
        if (rbCanopy != null)
        {
            Vector3 start           = rbCanopy.position;
            float   effectiveHeight = start.y - 5f;        // canopy stops at y=5, not y=0
            float   forwardDist     = 2.5f * effectiveHeight;
            float   headingRad  = rbCanopy.rotation.eulerAngles.y * Mathf.Deg2Rad;
            Vector3 landingPos  = new Vector3(
                start.x + Mathf.Sin(headingRad) * forwardDist,
                0f,
                start.z + Mathf.Cos(headingRad) * forwardDist
            );
            LandingZoneMarker marker = FindFirstObjectByType<LandingZoneMarker>();
            if (marker != null)
            {
                marker.transform.position = landingPos;
                Debug.Log($"[PlayerMovement] Landing zone placed at {landingPos}, heading {rbCanopy.rotation.eulerAngles.y:F1}°");
            }
        }

        // Store starting positions for restart
        _initCanopyPos = rbCanopy != null ? rbCanopy.position : Vector3.zero;
        _initBodyPos   = rbBody   != null ? rbBody.position   : Vector3.zero;

        Debug.Log("PlayerMovement ready. Platform: " + Application.platform);
        currentState.RbToState(rbCanopy, rbBody);
    }

    void Update()
    {
        bool triggered = false;

        // Keyboard: R key (Editor)
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            triggered = true;

        // Quest: A button on right controller (edge detect)
        var rightCtrl = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        if (rightCtrl.isValid)
        {
            rightCtrl.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool aDown);
            if (aDown && !_aWasDown) triggered = true;
            _aWasDown = aDown;
        }

        if (triggered) Restart();
    }

    void Restart()
    {
        _landed = false;
        rbCanopy.position        = _initCanopyPos;
        rbBody.position          = _initBodyPos;
        rbCanopy.linearVelocity  = Vector3.zero;
        rbCanopy.angularVelocity = Vector3.zero;
        rbBody.linearVelocity    = Vector3.zero;
        rbBody.angularVelocity   = Vector3.zero;
        currentState.RbToState(rbCanopy, rbBody);
        Debug.Log("[PlayerMovement] Restarted.");
    }

    void FixedUpdate()
    {
        if (rbCanopy == null || rbBody == null) return;
        if (_landed) return;

        currentState.RbToState(rbCanopy, rbBody);
        nextState = GetNextStepState(currentState);

        // Body hits ground → freeze everything
        if (nextState.BodyPos.y <= 0f)
        {
            _landed = true;
            rbCanopy.MovePosition(new Vector3(nextState.CanopyPos.x, 5f,  nextState.CanopyPos.z));
            rbBody.MovePosition(  new Vector3(nextState.BodyPos.x,   0f,  nextState.BodyPos.z));
            rbCanopy.linearVelocity  = Vector3.zero;
            rbCanopy.angularVelocity = Vector3.zero;
            rbBody.linearVelocity    = Vector3.zero;
            rbBody.angularVelocity   = Vector3.zero;
            Debug.Log("[PlayerMovement] Landed!");
            return;
        }

        rbCanopy.MovePosition(nextState.CanopyPos);
        rbBody.MovePosition(nextState.BodyPos);
        rbCanopy.MoveRotation(Quaternion.Euler(nextState.CanopyRot));
        rbBody.MoveRotation(Quaternion.Euler(nextState.BodyRot));

        currentState = nextState;
    }

    private State GetNextStepState(State current)
    {
        double t  = Time.time;
        double dt = Time.fixedDeltaTime;

        double[] xCurrent    = current.StateToVec();
        double[] xNext       = new double[24];
        int[]    xCurSize    = new int[] { 24 };
        int[]    xNextSize   = new int[] { 0 };

        CallEOM(t, xCurrent, xCurSize, dt, xNext, xNextSize);

        State next = new State();
        next.VecToState(xNext);
        return next;
    }

    // Returns [rightToggle, leftToggle, lookUp, lookDown, lookRight, lookLeft]
    // (kept for API compatibility — CallEOM reads input directly on Android)
    public double[] GetUserInput()
    {
        double delteRight = 0, delteLeft = 0;
        double deltaLookUp = 0, deltaLookDown = 0, deltaLookRight = 0, deltaLookLeft = 0;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed) delteRight = 1;
            if (Keyboard.current.aKey.isPressed) delteLeft  = 1;
        }

        return new double[] { delteRight, delteLeft, deltaLookUp, deltaLookDown, deltaLookRight, deltaLookLeft };
    }

    public class State
    {
        public Vector3 CanopyPos;
        public Vector3 CanopyRot;   // Euler angles (degrees)
        public Vector3 BodyPos;
        public Vector3 BodyRot;     // Euler angles (degrees)
        public Vector3 CanopyLinVel;
        public Vector3 CanopyAngVel;
        public Vector3 BodyLinVel;
        public Vector3 BodyAngVel;

        public void RbToState(Rigidbody rbCanopy, Rigidbody rbBody)
        {
            if (rbCanopy != null)
            {
                CanopyPos    = rbCanopy.position;
                CanopyRot    = rbCanopy.rotation.eulerAngles;
                CanopyLinVel = rbCanopy.linearVelocity;
                CanopyAngVel = rbCanopy.angularVelocity;
            }
            if (rbBody != null)
            {
                BodyPos    = rbBody.position;
                BodyRot    = rbBody.rotation.eulerAngles;
                BodyLinVel = rbBody.linearVelocity;
                BodyAngVel = rbBody.angularVelocity;
            }
        }

        public void VecToState(double[] v)
        {
            CanopyPos    = new Vector3((float)v[0],  (float)v[1],  (float)v[2]);
            CanopyRot    = new Vector3((float)v[3],  (float)v[4],  (float)v[5]);
            CanopyLinVel = new Vector3((float)v[6],  (float)v[7],  (float)v[8]);
            CanopyAngVel = new Vector3((float)v[9],  (float)v[10], (float)v[11]);
            BodyPos      = new Vector3((float)v[12], (float)v[13], (float)v[14]);
            BodyRot      = new Vector3((float)v[15], (float)v[16], (float)v[17]);
            BodyLinVel   = new Vector3((float)v[18], (float)v[19], (float)v[20]);
            BodyAngVel   = new Vector3((float)v[21], (float)v[22], (float)v[23]);
        }

        public double[] StateToVec()
        {
            return new double[]
            {
                CanopyPos.x,    CanopyPos.y,    CanopyPos.z,
                CanopyRot.x,    CanopyRot.y,    CanopyRot.z,
                CanopyLinVel.x, CanopyLinVel.y, CanopyLinVel.z,
                CanopyAngVel.x, CanopyAngVel.y, CanopyAngVel.z,
                BodyPos.x,      BodyPos.y,      BodyPos.z,
                BodyRot.x,      BodyRot.y,      BodyRot.z,
                BodyLinVel.x,   BodyLinVel.y,   BodyLinVel.z,
                BodyAngVel.x,   BodyAngVel.y,   BodyAngVel.z,
            };
        }
    }

    public class Control
    {
        public double toggleRight;
        public double toggleLeft;
        public double toggleFrontRisers;
        public double toggleCG;

        public Control()
        {
            toggleRight = toggleLeft = toggleFrontRisers = toggleCG = 0;
        }

        public static Control Add(Control c1, Control c2) => new Control
        {
            toggleRight       = c1.toggleRight       + c2.toggleRight,
            toggleLeft        = c1.toggleLeft        + c2.toggleLeft,
            toggleFrontRisers = c1.toggleFrontRisers + c2.toggleFrontRisers,
            toggleCG          = c1.toggleCG          + c2.toggleCG,
        };
    }
}
