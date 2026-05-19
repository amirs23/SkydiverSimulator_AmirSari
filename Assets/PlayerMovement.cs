using UnityEngine;
using UnityEngine.InputSystem;
using System.Runtime.InteropServices;

// Attach to any GameObject in the scene.
// Drag the canopy and body Rigidbodies into the Inspector slots.
// EOM_Solver.dll is Windows-only — on Mac/Quest the physics step is skipped (state held constant).
public class PlayerMovement : MonoBehaviour
{
    public Rigidbody rbCanopy;
    public Rigidbody rbBody;
    public Transform playerTransform;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
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
    // EOM_Solver.dll is Windows-only — pass state through unchanged on other platforms
    private static void CallEOM(double t, double[] xCur, int[] xCurSize, double dt, double[] xNext, int[] xNextSize)
        => System.Array.Copy(xCur, xNext, xCur.Length);
#endif

    private State currentState = new State();
    private State nextState    = new State();

    void Start()
    {
        if (rbCanopy != null) rbCanopy.useGravity = false;
        if (rbBody   != null) rbBody.useGravity   = false;
        Debug.Log("PlayerMovement ready. Platform: " + Application.platform);
        currentState.RbToState(rbCanopy, rbBody);
    }

    void FixedUpdate()
    {
        if (rbCanopy == null || rbBody == null) return;

        currentState.RbToState(rbCanopy, rbBody);
        nextState = GetNextStepState(currentState);

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
                CanopyRot    = rbCanopy.rotation.eulerAngles; // was: quaternion components (bug)
                CanopyLinVel = rbCanopy.linearVelocity;
                CanopyAngVel = rbCanopy.angularVelocity;
            }
            if (rbBody != null)
            {
                BodyPos    = rbBody.position;
                BodyRot    = rbBody.rotation.eulerAngles;     // was: quaternion components (bug)
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
