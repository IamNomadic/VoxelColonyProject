using UnityEngine;

// Formerly "ExternalCameraFlightRig_CustomControls_Updated"
// Now purely handles Physics and Input for movement.
public class PlayerMovement : MonoBehaviour
{
    [Header("External Camera")]
    public Transform externalCamera;
    public Vector3 cameraLocalOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float sprintMultiplier = 3f;
    public float verticalSpeed = 8f;

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
    private float yaw;
    private float pitch;
    private Vector3 desiredVelocity;
    private bool isSprinting;

    // --- CONTROL FLAGS ---
    public bool InputLocked { get; set; } = false;

    void Awake()
    {
        if (externalCamera == null && Camera.main != null) externalCamera = Camera.main.transform;

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true; // We move manually
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (collisionMask == 0) collisionMask = -1;

        yaw = transform.eulerAngles.y;
        if (externalCamera != null)
        {
            float camPitch = externalCamera.eulerAngles.x;
            pitch = camPitch > 180f ? camPitch - 360f : camPitch;
        }
    }

    void Update()
    {
        if (InputLocked)
        {
            desiredVelocity = Vector3.zero;
            return;
        }

        HandleLook();
        HandleMove();
    }

    void HandleLook()
    {
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y") * (invertY ? 1f : -1f);

        yaw += mx * mouseSensitivity;
        pitch += my * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void HandleMove()
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

        // Flight Mode Logic
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 horizontal = (camForward * forwardInput + camRight * strafeInput);
        if (horizontal.sqrMagnitude > 1f) horizontal.Normalize();

        desiredVelocity = (horizontal * targetSpeed) + (Vector3.up * (up * verticalSpeed));
    }

    void FixedUpdate()
    {
        // Physics collision logic
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

        if (externalCamera != null)
        {
            externalCamera.position = rb.position + rigRot * cameraLocalOffset;
            externalCamera.rotation = rigRot * Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}