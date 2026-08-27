using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("External Camera")]
    public Transform externalCamera;
    public Vector3 cameraLocalOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Universal Movement Settings")]
    public float moveSpeed = 6f;
    public float sprintMultiplier = 1.5f;

    [Header("Simulation Integration")]
    public bool obeysSimulationClock = false; // NEW: Determines if we use real-time or sim-time

    [Header("Survival Settings (Gravity)")]
    public float jumpSpeed = 8f;
    public float gravity = -20f;

    [Header("Creative Settings (Flight)")]
    public float verticalFlySpeed = 8f;

    [Header("Look Settings")]
    public float mouseSensitivity = 4f;
    public bool invertY = false;
    public float minPitch = -89f;
    public float maxPitch = 89f;

    [Header("Physics")]
    public float playerRadius = 1f;
    public LayerMask collisionMask;

    // State
    public bool isFlying { get; private set; }
    private Rigidbody rb;
    private CharacterController charCtrl;
    private float yaw;
    private float pitch;
    private Vector3 desiredVelocity;
    private float verticalVelocity = 0f;

    public bool InputLocked { get; set; } = false;

    void Awake()
    {
        if (externalCamera == null && Camera.main != null) externalCamera = Camera.main.transform;

        if (externalCamera != null)
        {
            Camera cam = externalCamera.GetComponent<Camera>();
            if (cam != null) cam.nearClipPlane = 0.01f;
        }

        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        charCtrl = GetComponent<CharacterController>();
        charCtrl.radius = playerRadius;
        charCtrl.height = 1.65f;
        charCtrl.center = new Vector3(0, 0.5f, 0);
        charCtrl.stepOffset = 0.2f;

        if (collisionMask == 0) collisionMask = -1;

        yaw = transform.eulerAngles.y;
        if (externalCamera != null)
        {
            float camPitch = externalCamera.eulerAngles.x;
            pitch = camPitch > 180f ? camPitch - 360f : camPitch;
        }
    }

    public void SetFlying(bool state)
    {
        isFlying = state;
        if (charCtrl != null) charCtrl.enabled = !isFlying;
        verticalVelocity = 0f;
    }

    void Update()
    {
        if (CommandConsole.IsOpen) return;
        if (InputLocked)
        {
            desiredVelocity = Vector3.zero;
            return;
        }

        HandleLook(); // Look is always real-time so the mouse doesn't feel broken

        if (isFlying) HandleFlyMove();
        else HandleWalkMove();
    }

    void HandleLook()
    {
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y") * (invertY ? 1f : -1f);

        yaw += mx * mouseSensitivity;
        pitch += my * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion rigRot = Quaternion.Euler(0f, yaw, 0f);
        if (externalCamera != null) externalCamera.rotation = rigRot * Quaternion.Euler(pitch, 0f, 0f);
        if (!isFlying) transform.rotation = rigRot;
    }

    void HandleFlyMove()
    {
        float forwardInput = Input.GetAxisRaw("Vertical");
        float strafeInput = Input.GetAxisRaw("Horizontal");
        float up = (Input.GetKey(KeyCode.Space) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftControl) ? 1f : 0f);

        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * sprintMultiplier : moveSpeed;

        Vector3 camForward = externalCamera.forward;
        Vector3 camRight = externalCamera.right;
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 horizontal = (camForward * forwardInput + camRight * strafeInput).normalized;
        desiredVelocity = (horizontal * currentSpeed) + (Vector3.up * (up * verticalFlySpeed));
    }

    void HandleWalkMove()
    {
        // Use Simulation Clock if active, otherwise use Unity real-time
        float dt = (obeysSimulationClock && SimulationClock.Instance != null)
            ? SimulationClock.Instance.SimulationDeltaTime
            : Time.deltaTime;

        float forwardInput = Input.GetAxisRaw("Vertical");
        float strafeInput = Input.GetAxisRaw("Horizontal");

        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * sprintMultiplier : moveSpeed;

        Vector3 camForward = externalCamera.forward;
        Vector3 camRight = externalCamera.right;
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 moveDir = (camForward * forwardInput + camRight * strafeInput).normalized * currentSpeed;

        if (charCtrl.isGrounded)
        {
            verticalVelocity = -2f;
            // Prevent jumping if paused
            if (dt > 0 && Input.GetKeyDown(KeyCode.Space)) verticalVelocity = jumpSpeed;
        }
        else
        {
            verticalVelocity += gravity * dt;
        }

        moveDir.y = verticalVelocity;

        // This will equal 0 movement if the simulation is paused!
        if (charCtrl.enabled) charCtrl.Move(moveDir * dt);

        rb.position = transform.position;
    }

    void FixedUpdate()
    {
        if (isFlying)
        {
            Vector3 displacement = desiredVelocity * Time.fixedDeltaTime;
            Vector3 finalPos = rb.position;

            if (displacement.magnitude > 0.001f)
            {
                if (Physics.SphereCast(rb.position, playerRadius, displacement.normalized, out RaycastHit hit, displacement.magnitude, collisionMask))
                    finalPos = rb.position + (displacement.normalized * Mathf.Max(0, hit.distance - 0.01f));
                else
                    finalPos = rb.position + displacement;
            }

            rb.MovePosition(finalPos);
            rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
        }
    }

    void LateUpdate()
    {
        if (externalCamera != null) externalCamera.position = transform.position + Quaternion.Euler(0f, yaw, 0f) * cameraLocalOffset;
    }
}