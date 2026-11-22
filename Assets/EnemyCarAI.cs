using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyCarAI : MonoBehaviour
{
    [Header("Маршрут")]
    public Transform[] waypoints;
    public float waypointThreshold = 6f;
    public bool loop = true;

    [Header("Движение")]
    public float maxMotorTorque = 900f;
    public float brakeForce = 2500f;
    public float maxSpeed = 25f;          // м/с (25 = 90 км/ч)
    public float minCornerSpeed = 8f;     // скорость в самом резком повороте
    public bool allWheelDrive = true;

    [Header("Руль")]
    public float maxSteerAngle = 30f;
    public float highSpeedSteerFactor = 0.35f; // доля угла руля на макс. скорости
    public float steerSmoothing = 6f;

    [Header("Устойчивость")]
    public Vector3 centerOfMass = new Vector3(0f, -0.7f, 0f);
    public float downforce = 40f;         // прижимная сила, растёт со скоростью
    public float antiRollForce = 6000f;   // стабилизатор поперечной устойчивости

    [Header("Восстановление")]
    public float flipTimeToReset = 2f;
    public float stuckTimeToReset = 5f;

    [Header("Колёса (коллайдеры)")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    [Header("Колёса (меши)")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    private Rigidbody rb;
    private int currentIndex = 0;
    private float flipTimer;
    private float stuckTimer;
    private float currentSteer;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = centerOfMass;
    }

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        float speed = rb.velocity.magnitude;

        Transform target = waypoints[currentIndex];
        Transform next = waypoints[GetNextIndex(currentIndex)];

        // --- Направление считаем только в горизонтальной плоскости (без разницы по высоте) ---
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        Vector3 toNext = next.position - target.position;
        toNext.y = 0f;

        // --- Руль ---
        Vector3 localDir = transform.InverseTransformDirection(toTarget.normalized);
        float angleToTarget = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;

        float speedFactor = Mathf.Lerp(1f, highSpeedSteerFactor, Mathf.Clamp01(speed / maxSpeed));
        float desiredSteer = Mathf.Clamp(angleToTarget, -maxSteerAngle, maxSteerAngle) * speedFactor;
        currentSteer = Mathf.Lerp(currentSteer, desiredSteer, Time.fixedDeltaTime * steerSmoothing);
        frontLeft.steerAngle = currentSteer;
        frontRight.steerAngle = currentSteer;

        // --- Целевая скорость: зависит от резкости поворота впереди и от угла до цели ---
        float cornerAngle = toNext.sqrMagnitude > 0.01f ? Vector3.Angle(toTarget, toNext) : 0f;
        float cornerSharpness = Mathf.Clamp01(cornerAngle / 90f);
        float turnSharpness = Mathf.Clamp01(Mathf.Abs(angleToTarget) / 60f);
        float sharpness = Mathf.Max(cornerSharpness, turnSharpness);
        float targetSpeed = Mathf.Lerp(maxSpeed, minCornerSpeed, sharpness);

        // --- Газ / тормоз ---
        float motor = 0f;
        float brake = 0f;

        if (speed > targetSpeed + 1f)
        {
            float overspeed = Mathf.Clamp01((speed - targetSpeed) / 10f);
            brake = brakeForce * overspeed;
        }
        else
        {
            motor = maxMotorTorque * Mathf.Clamp01((targetSpeed - speed) / targetSpeed + 0.2f);
        }

        ApplyMotorAndBrake(motor, brake);

        // --- Смена вейпоинта ---
        if (toTarget.magnitude < waypointThreshold)
        {
            currentIndex = GetNextIndex(currentIndex);
        }

        // --- Стабилизация ---
        rb.AddForce(-transform.up * downforce * speed);
        ApplyAntiRoll(frontLeft, frontRight);
        ApplyAntiRoll(rearLeft, rearRight);

        // --- Восстановление после переворота / застревания ---
        CheckRecovery(speed);

        UpdateWheelsVisuals();
    }

    int GetNextIndex(int index)
    {
        int n = index + 1;
        if (n >= waypoints.Length) n = loop ? 0 : waypoints.Length - 1;
        return n;
    }

    void ApplyMotorAndBrake(float motor, float brake)
    {
        frontLeft.motorTorque = motor;
        frontRight.motorTorque = motor;
        rearLeft.motorTorque = allWheelDrive ? motor : 0f;
        rearRight.motorTorque = allWheelDrive ? motor : 0f;

        frontLeft.brakeTorque = brake;
        frontRight.brakeTorque = brake;
        rearLeft.brakeTorque = brake;
        rearRight.brakeTorque = brake;
    }

    // Стабилизатор поперечной устойчивости: выравнивает ход подвески левого и правого колеса
    void ApplyAntiRoll(WheelCollider left, WheelCollider right)
    {
        float travelL = 1f;
        float travelR = 1f;

        WheelHit hit;
        bool groundedL = left.GetGroundHit(out hit);
        if (groundedL)
            travelL = (-left.transform.InverseTransformPoint(hit.point).y - left.radius) / left.suspensionDistance;

        bool groundedR = right.GetGroundHit(out hit);
        if (groundedR)
            travelR = (-right.transform.InverseTransformPoint(hit.point).y - right.radius) / right.suspensionDistance;

        float force = (travelL - travelR) * antiRollForce;

        if (groundedL)
            rb.AddForceAtPosition(left.transform.up * -force, left.transform.position);
        if (groundedR)
            rb.AddForceAtPosition(right.transform.up * force, right.transform.position);
    }

    void CheckRecovery(float speed)
    {
        // Перевёрнута
        if (Vector3.Dot(transform.up, Vector3.up) < 0.3f)
            flipTimer += Time.fixedDeltaTime;
        else
            flipTimer = 0f;

        // Стоит на месте
        if (speed < 0.5f)
            stuckTimer += Time.fixedDeltaTime;
        else
            stuckTimer = 0f;

        if (flipTimer > flipTimeToReset || stuckTimer > stuckTimeToReset)
            ResetToTrack();
    }

    void ResetToTrack()
    {
        int prev = (currentIndex - 1 + waypoints.Length) % waypoints.Length;
        Transform p = waypoints[prev];
        Transform t = waypoints[currentIndex];

        Vector3 dir = t.position - p.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = p.position + Vector3.up * 1f;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        flipTimer = 0f;
        stuckTimer = 0f;
        currentSteer = 0f;
    }

    void UpdateWheelsVisuals()
    {
        UpdateWheelPose(frontLeft, frontLeftMesh);
        UpdateWheelPose(frontRight, frontRightMesh);
        UpdateWheelPose(rearLeft, rearLeftMesh);
        UpdateWheelPose(rearRight, rearRightMesh);
    }

    void UpdateWheelPose(WheelCollider col, Transform mesh)
    {
        if (mesh == null) return;
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;
        mesh.rotation = rot;
    }

    void OnDrawGizmosSelected()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.TransformPoint(centerOfMass), 0.15f);

        if (waypoints != null && currentIndex < waypoints.Length && waypoints[currentIndex] != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, waypoints[currentIndex].position);
        }
    }
}