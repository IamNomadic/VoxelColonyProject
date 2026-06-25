using UnityEngine;

// Formerly "ExternalCameraFlightRig_CustomControls_Updated"
// Now handles BOTH custom Kinematic Flight and CharacterController Walking.
public class PlayerMovement : MonoBehaviour
{
    [Header("External Camera")]
    public Transform externalCamera;
    public Vector3 cameraLocalOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Movement Settings (Flight)")]
    public float moveSpeed = 10f;
    public float sprintMultiplier = 3f;
    public float verticalSpeed = 8f;

    [Header("Movement Settings (Survival)")]
    public bool isFlying = true;
    public float gravity = -20f;
    public float jumpSpeed = 8f;

    [Header("Look Settings")]
    public float mouseSensitivity = 4f;
    public bool invertY = false;
    public float minPitch = -89f;
    public float maxPitch = 89f;

    [Header("Physics")]
    public float playerRadius = 0.4f;
    public LayerMask collisionMask;

    // State
    private Rigidbody rb;
    private CharacterController charCtrl;
    private float yaw;
    private float pitch;
    private Vector3 desiredVelocity;
    private bool isSprinting;
    private float verticalVelocity = 0f;

    // --- CONTROL FLAGS ---
    public bool InputLocked { get; set; } = false;

    void Awake()
    {
        if (externalCamera == null && Camera.main != null) externalCamera = Camera.main.transform;

        // 1. Setup Rigidbody (Used for Flight)
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // 2. Setup CharacterController (Used for Walking/Survival)
        charCtrl = GetComponent<CharacterController>();
        if (charCtrl == null)
        {
            charCtrl = gameObject.AddComponent<CharacterController>();
            charCtrl.radius = playerRadius;
            charCtrl.height = 1.8f;
            charCtrl.center = new Vector3(0, 0.9f, 0);
            charCtrl.stepOffset = 0.6f;
        }

        if (collisionMask == 0) collisionMask = -1;

        yaw = transform.eulerAngles.y;
        if (externalCamera != null)
        {
            float camPitch = externalCamera.eulerAngles.x;
            pitch = camPitch > 180f ? camPitch - 360f : camPitch;
        }

        SetFlying(isFlying);
    }

    // Toggle between Flight (SphereCast) and Walking (CharacterController)
    public void SetFlying(bool state)
    {
        isFlying = state;
        if (charCtrl != null) charCtrl.enabled = !isFlying;
        if (rb != null) rb.isKinematic = true; // Always true, we manipulate it manually either way
    }

    void Update()
    {
        if (InputLocked)
        {
            desiredVelocity = Vector3.zero;
            return;
        }

        HandleLook();

        if (isFlying) HandleFlyMove();
        else HandleWalkMove();

        HandleVoidRescue();
    }

    void HandleLook()
    {
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y") * (invertY ? 1f : -1f);

        yaw += mx * mouseSensitivity;
        pitch += my * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Immediate rotation for smoothness
        Quaternion rigRot = Quaternion.Euler(0f, yaw, 0f);
        if (externalCamera != null)
        {
            externalCamera.rotation = rigRot * Quaternion.Euler(pitch, 0f, 0f);
        }

        if (!isFlying) transform.rotation = rigRot;
    }

    // --- YOUR ORIGINAL FLIGHT LOGIC (Untouched) ---
    void HandleFlyMove()
    {
        float forwardInput = Input.GetAxisRaw("Vertical");
        float strafeInput = Input.GetAxisRaw("Horizontal");

        float up = 0f;
        if (Input.GetKey(KeyCode.Space)) up += 1f;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.CapsLock)) up -= 1f;

        isSprinting = Input.GetKey(KeyCode.LeftShift);
        float targetSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        Vector3 camForward = externalCamera.forward;
        Vector3 camRight = externalCamera.right;

        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 horizontal = (camForward * forwardInput + camRight * strafeInput);
        if (horizontal.sqrMagnitude > 1f) horizontal.Normalize();

        desiredVelocity = (horizontal * targetSpeed) + (Vector3.up * (up * verticalSpeed));
    }

    // --- NEW SURVIVAL WALKING LOGIC ---
    void HandleWalkMove()
    {
        float forwardInput = Input.GetAxisRaw("Vertical");
        float strafeInput = Input.GetAxisRaw("Horizontal");

        isSprinting = Input.GetKey(KeyCode.LeftShift);
        float targetSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        Vector3 camForward = externalCamera.forward;
        Vector3 camRight = externalCamera.right;
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 moveDir = (camForward * forwardInput + camRight * strafeInput);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
        moveDir *= targetSpeed;

        // Jump & Gravity
        if (charCtrl.isGrounded)
        {
            verticalVelocity = -2f; // Slight downward force to stay glued to slopes
            if (Input.GetKeyDown(KeyCode.Space))
            {
                verticalVelocity = jumpSpeed;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        moveDir.y = verticalVelocity;

        if (charCtrl.enabled) charCtrl.Move(moveDir * Time.deltaTime);

        // Keep Rigidbody synced with CharacterController position
        rb.position = transform.position;
    }

    void FixedUpdate()
    {
        // YOUR ORIGINAL PHYSICS COLLISION FOR FLIGHT
        if (isFlying)
        {
            Vector3 displacement = desiredVelocity * Time.fixedDeltaTime;
            Vector3 finalPos = rb.position;

            if (displacement.magnitude > 0.001f)
            {
                if (Physics.SphereCast(rb.position, playerRadius, displacement.normalized, out RaycastHit hit, displacement.magnitude, collisionMask))
                {
                    float d = Mathf.Max(0, hit.distance - 0.01f);
                    finalPos = rb.position + (displacement.normalized * d);
                }
                else
                {
                    finalPos = rb.position + displacement;
                }
            }

            rb.MovePosition(finalPos);

            Quaternion rigRot = Quaternion.Euler(0f, yaw, 0f);
            rb.MoveRotation(rigRot);
        }
    }

    void LateUpdate()
    {
        // Glue the camera to the player so it doesn't jitter during physics updates
        if (externalCamera != null)
        {
            Quaternion rigRot = Quaternion.Euler(0f, yaw, 0f);
            externalCamera.position = transform.position + rigRot * cameraLocalOffset;
        }
    }

    void HandleVoidRescue()
    {
        // Rescues the player if they fall below the world while chunks are loading
        if (transform.position.y < -30f)
        {
            if (charCtrl != null) charCtrl.enabled = false;
            transform.position = new Vector3(transform.position.x, Chunk.CHUNK_HEIGHT + 20f, transform.position.z);
            verticalVelocity = 0f;
            if (!isFlying && charCtrl != null) charCtrl.enabled = true;
        }
    }
}