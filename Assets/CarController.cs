using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Сила движения")]
    public float motorTorque = 1500f;
    public float maxSteerAngle = 12f;
    public float brakeForce = 3000f;

    [Header("Колёса")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    [Header("Визуальные колёса")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    [Header("Устойчивость и подвеска")]
    public Vector3 centerOfMassOffset = new Vector3(0, -0.9f, 0);
    public float angularDragValue = 5f;

    public float suspensionSpring = 35000f;
    public float suspensionDamper = 4500f;
    public float suspensionDistance = 0.2f;

    private float steerInput;
    private float motorInput;
    private bool isBraking;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.centerOfMass += centerOfMassOffset;
        rb.angularDrag = angularDragValue;

        SetupWheelSuspension(frontLeft);
        SetupWheelSuspension(frontRight);
        SetupWheelSuspension(rearLeft);
        SetupWheelSuspension(rearRight);
    }

    void FixedUpdate()
    {
        HandleInput();
        ApplyMotor();
        ApplySteering();
        UpdateWheels();
    }

    void HandleInput()
    {
        motorInput = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");
        isBraking = Input.GetKey(KeyCode.Space);
    }

    void ApplyMotor()
    {
        float currentBrake = isBraking ? brakeForce : 0f;

        frontLeft.motorTorque = motorInput * motorTorque;
        frontRight.motorTorque = motorInput * motorTorque;

        frontLeft.brakeTorque = currentBrake;
        frontRight.brakeTorque = currentBrake;
        rearLeft.brakeTorque = currentBrake;
        rearRight.brakeTorque = currentBrake;
    }

    void ApplySteering()
    {
        float targetSteerAngle = steerInput * maxSteerAngle;

        frontLeft.steerAngle = Mathf.Lerp(frontLeft.steerAngle, targetSteerAngle, Time.deltaTime * 5f);
        frontRight.steerAngle = Mathf.Lerp(frontRight.steerAngle, targetSteerAngle, Time.deltaTime * 5f);
    }

    void UpdateWheels()
    {
        UpdateWheelPose(frontLeft, frontLeftMesh);
        UpdateWheelPose(frontRight, frontRightMesh);
        UpdateWheelPose(rearLeft, rearLeftMesh);
        UpdateWheelPose(rearRight, rearRightMesh);
    }

    void UpdateWheelPose(WheelCollider collider, Transform mesh)
    {
        if (mesh == null) return;
        Vector3 pos;
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);
        mesh.position = pos;
        mesh.rotation = rot;
    }

    void SetupWheelSuspension(WheelCollider wheel)
    {
        JointSpring spring = wheel.suspensionSpring;
        spring.spring = suspensionSpring;
        spring.damper = suspensionDamper;
        wheel.suspensionSpring = spring;

        wheel.suspensionDistance = suspensionDistance;
    }

   
    public void ApplyFullBrake()
    {
        frontLeft.motorTorque = 0f;
        frontRight.motorTorque = 0f;

        frontLeft.brakeTorque = brakeForce;
        frontRight.brakeTorque = brakeForce;
        rearLeft.brakeTorque = brakeForce;
        rearRight.brakeTorque = brakeForce;
    }
}
