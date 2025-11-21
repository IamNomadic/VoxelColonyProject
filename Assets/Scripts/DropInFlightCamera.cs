using UnityEngine;

[AddComponentMenu("Debug/External Camera Flight Rig - Custom Controls Updated")]
public class ExternalCameraFlightRig_CustomControls_Updated : MonoBehaviour
{
    [Header("Interaction")]
    public float interactionRange = 8f;
    public BlockData blockToPlace; // Assign a BlockData asset here to place it!

    // Reference to the world (auto-found)
    private VoxelWorld voxelWorld;

    [Header("External Camera")]
    public Transform externalCamera;
    public Vector3 cameraLocalOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Movement")]
    public float moveSpeed = 10f;
    public float sprintMultiplier = 3f;
    public float verticalSpeed = 8f;

    [Header("Look")]
    public float mouseSensitivity = 4f;
    public bool invertY = false;
    public float minPitch = -89f;
    public float maxPitch = 89f;
    public bool lockCursor = true;

    Rigidbody rb;
    float yaw;
    float pitch;
    Vector3 desiredVelocity;

    void Awake()
    {
        voxelWorld = FindObjectOfType<VoxelWorld>();

        if (externalCamera == null && Camera.main != null)
            externalCamera = Camera.main.transform;

        if (externalCamera == null)
        {
            Debug.LogError("No externalCamera assigned and no Camera.main found.");
            enabled = false;
            return;
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.detectCollisions = false;
        rb.constraints = RigidbodyConstraints.None;

        yaw = transform.eulerAngles.y;
        float camPitch = externalCamera.eulerAngles.x;
        pitch = camPitch > 180f ? camPitch - 360f : camPitch;
    }

    void OnEnable()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        HandleInput();
        HandleInteraction();
    }

    void HandleInteraction()
    {
        // We need the VoxelWorld to do anything
        if (voxelWorld == null) return;

        // Raycast from camera center
        Ray ray = new Ray(externalCamera.position, externalCamera.forward);

        // Left Click: Break (Set to Null)
        if (Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(ray, out RaycastHit hit, interactionRange))
            {
                // Move slightly INSIDE the block to get the coordinate of the block we hit
                Vector3 targetPos = hit.point - (hit.normal * 0.1f);
                voxelWorld.ModifyBlock(targetPos, null);
            }
        }

        // Right Click: Place (Set to blockToPlace)
        if (Input.GetMouseButtonDown(1))
        {
            if (blockToPlace != null && Physics.Raycast(ray, out RaycastHit hit, interactionRange))
            {
                // Move slightly OUTSIDE the block (along normal) to find the empty space adjacent
                Vector3 targetPos = hit.point + (hit.normal * 0.1f);

                // Optional: Don't place if player is inside that block
                // Bounds playerBounds = GetComponent<Collider>().bounds;
                // if (!playerBounds.Contains(targetPos)) ...

                voxelWorld.ModifyBlock(targetPos, blockToPlace);
            }
        }
    }

    void HandleInput()
    {
        // Look input
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y") * (invertY ? 1f : -1f);

        yaw += mx * mouseSensitivity;
        pitch += my * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Movement input
        float forwardInput = Input.GetAxis("Vertical");
        float rawStrafeInput = Input.GetAxis("Horizontal");
        float strafeInput = -rawStrafeInput; // Inverted for this specific rig style if needed

        // Vertical input
        float up = 0f;
        if (Input.GetKey(KeyCode.Space)) up += 1f;
        bool descend = Input.GetKey(KeyCode.CapsLock) || Input.GetKey(KeyCode.LeftControl);
        if (descend) up -= 1f;

        // Sprint
        float targetSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            targetSpeed *= sprintMultiplier;

        // Determine camera forward projected onto horizontal plane
        Vector3 camForward = externalCamera.forward;
        camForward.y = 0f;
        if (camForward.sqrMagnitude < 0.0001f) camForward = transform.forward;
        camForward.Normalize();

        Vector3 camRight = Vector3.Cross(Vector3.up, -camForward).normalized;

        Vector3 horizontalMove = (camForward * forwardInput + camRight * strafeInput);
        Vector3 horizVelocity = horizontalMove.sqrMagnitude > 0.000001f ? horizontalMove.normalized * targetSpeed : Vector3.zero;
        Vector3 verticalVelocity = Vector3.up * (up * verticalSpeed);

        desiredVelocity = horizVelocity + verticalVelocity;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void FixedUpdate()
    {
        Vector3 nextPos = rb.position + desiredVelocity * Time.fixedDeltaTime;
        rb.MovePosition(nextPos);

        Quaternion rigRot = Quaternion.Euler(0f, yaw, 0f);
        rb.MoveRotation(rigRot);

        Vector3 worldCamPos = rb.position + rigRot * cameraLocalOffset;
        externalCamera.position = worldCamPos;
        Quaternion camLocalPitch = Quaternion.Euler(pitch, 0f, 0f);
        externalCamera.rotation = rigRot * camLocalPitch;
    }
}