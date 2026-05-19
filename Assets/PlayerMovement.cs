using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using System.Runtime.InteropServices;

public class PlayerMovement : MonoBehaviour
{
    public Rigidbody rbCanopy;
    public Rigidbody rbBody;
    private State currentState;
    private State nextState;
    public Transform playerTransform;

    [DllImport("EOM_Solver")]
    public static extern void EOM_Solver(
        double tCurrent,
        double[] xCurrentUnity_data,
        int[] xCurrentUnity_size,
        double dt,
        double[] xNextUnity_data,
        int[] xNextUnity_size
    );
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rbCanopy.useGravity = false;
        rbBody.useGravity = false;
        Debug.Log("Player is ready");
        currentState = new State();
        nextState = new State();

        currentState.RbToState(rbCanopy, rbBody);

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        currentState.RbToState(rbCanopy, rbBody);
        nextState = GetNextStepState(currentState);

        rbCanopy.MovePosition(nextState.CanopyPos);
        rbBody.MovePosition(nextState.BodyPos);
        rbCanopy.MoveRotation(Quaternion.Euler(nextState.CanopyRot));
        rbBody.MoveRotation(Quaternion.Euler(nextState.BodyRot));

        Camera.main.transform.position = nextState.CanopyPos - new Vector3(0,0,5);
        Camera.main.transform.rotation = Quaternion.Euler(0,0,0);

        currentState = nextState;

    }

    private void LateUpdate()
    {
        if (playerTransform != null)
        {
        }
    }

    private State GetInitState()
    {

        State initState = new State();


        return initState;
    }


    private State GetNextStepState(State currentStateHere)
    {

        State nextStateHere = currentStateHere;
        double timeStep = Time.deltaTime;     

        // === Call Matlab here ===
        double t = Time.time;
        double dt = Time.deltaTime;

        double[] xCurrent = currentStateHere.StateToVec();
        double[] xNext = new double[24];
        int[] xCurrentSize = new int[1] { 24 };
        int[] xNextSize = new int[1] { 0 };

        EOM_Solver(t, xCurrent, xCurrentSize, dt, xNext, xNextSize);
        nextStateHere.VecToState(xNext);

       // nextState = currentState;
        // ========================

        return nextStateHere;
    }

    public double[] GetUserInput()
    {
        double delteRight = 0;
        double delteLeft = 0;      
        double deltaLookUp = 0;
        double deltaLookDown = 0;       
        double deltaLookRight = 0;
        double deltaLookLeft = 0;

        if (Keyboard.current.dKey.isPressed) // Right turn
        {
            delteRight = 1;
        }

        if (Keyboard.current.aKey.isPressed) // Left turn
        {
            delteLeft = 1;
        }

        //if (Keyboard.current.spaceKey.isPressed)
        //{
        //    if (rbCanopy.position.y <= 0.6)
        //    {
        //        rbCanopy.AddForce(0, 10000 * Time.deltaTime, 0);
        //        Debug.Log("Space is pressed");
        //    }
        //}




        double[] playerInput = { delteRight, delteLeft, deltaLookUp, deltaLookDown, deltaLookRight, deltaLookLeft };



        return playerInput;
    }


    public class State
    {
        public Vector3 CanopyPos;
        public Vector3 CanopyRot;
        public Vector3 BodyPos;
        public Vector3 BodyRot;
        public Vector3 CanopyLinVel;
        public Vector3 CanopyAngVel;
        public Vector3 BodyLinVel;
        public Vector3 BodyAngVel;

        public void RbToState(Rigidbody rbCanopy, Rigidbody rbBody)
        {
            CanopyPos = new Vector3(rbCanopy.position.x, rbCanopy.position.y, rbCanopy.position.z);
            CanopyRot = new Vector3(rbCanopy.rotation.x, rbCanopy.rotation.y, rbCanopy.rotation.z);
            CanopyLinVel = rbCanopy.linearVelocity;
            CanopyAngVel = rbCanopy.angularVelocity;
            BodyPos = new Vector3(rbBody.position.x, rbBody.position.y, rbBody.position.z);
            BodyRot = new Vector3(rbBody.rotation.x, rbBody.rotation.y, rbBody.rotation.z);
            BodyLinVel = rbBody.linearVelocity;
            BodyAngVel = rbBody.angularVelocity;
        }

        public void VecToState(double[] inVec)
        {
            this.CanopyPos = new Vector3((float)inVec[0], (float)inVec[1], (float)inVec[2]);
            this.CanopyRot = new Vector3((float)inVec[3], (float)inVec[4], (float)inVec[5]);
            this.CanopyLinVel = new Vector3((float)inVec[6], (float)inVec[7], (float)inVec[8]);
            this.CanopyAngVel = new Vector3((float)inVec[9], (float)inVec[10], (float)inVec[11]);
            this.BodyPos = new Vector3((float)inVec[12], (float)inVec[13], (float)inVec[14]);
            this.BodyRot = new Vector3((float)inVec[15], (float)inVec[16], (float)inVec[17]);
            this.BodyLinVel = new Vector3((float)inVec[18], (float)inVec[19], (float)inVec[20]);
            this.BodyAngVel = new Vector3((float)inVec[21], (float)inVec[22], (float)inVec[23]);
        }

        public double[] StateToVec()
        {
            double[] stateVec = new double[24];
            stateVec[0] = this.CanopyPos[0];
            stateVec[1] = this.CanopyPos[1];
            stateVec[2] = this.CanopyPos[2];
            stateVec[3] = this.CanopyRot[0];
            stateVec[4] = this.CanopyRot[1];
            stateVec[5] = this.CanopyRot[2];
            stateVec[6] = this.CanopyLinVel[0];
            stateVec[7] = this.CanopyLinVel[1];
            stateVec[8] = this.CanopyLinVel[2];
            stateVec[9] = this.CanopyAngVel[0];
            stateVec[10] = this.CanopyAngVel[1];
            stateVec[11] = this.CanopyAngVel[2];
            stateVec[12] = this.BodyPos[0];
            stateVec[13] = this.BodyPos[1];
            stateVec[14] = this.BodyPos[2];
            stateVec[15] = this.BodyRot[0];
            stateVec[16] = this.BodyRot[1];
            stateVec[17] = this.BodyRot[2];
            stateVec[18] = this.BodyLinVel[0];
            stateVec[19] = this.BodyLinVel[1];
            stateVec[20] = this.BodyLinVel[2];
            stateVec[21] = this.BodyAngVel[0];
            stateVec[22] = this.BodyAngVel[1];
            stateVec[23] = this.BodyAngVel[2];
            return stateVec;
        }

    }

    public class Control
    {
        public double toggleRight; // Right toggle pull
        public double toggleLeft; // Left toggle pull
        public double toggleFrontRisers; // Front risers pull
        public double toggleCG; // moving C.G sideways. Positive to right

        public Control()
        {
            toggleRight = 0;
            toggleLeft = 0;
            toggleFrontRisers = 0;
            toggleCG = 0;
        }
        
        public static Control Add(Control control1, Control control2)
        {
            Control control3 = new Control();
            control3.toggleRight = control1.toggleRight + control2.toggleRight;
            control3.toggleLeft = control1.toggleLeft + control2.toggleLeft;
            control3.toggleFrontRisers = control1.toggleFrontRisers + control2.toggleFrontRisers;
            control3.toggleCG = control1.toggleCG + control2.toggleCG;


            return control3;
        }

    }


}
