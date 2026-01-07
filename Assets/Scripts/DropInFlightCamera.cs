using UnityEngine;

[AddComponentMenu("Debug/External Camera Flight Rig - Snappy Movement")]
public class ExternalCameraFlightRig_CustomControls_Updated : MonoBehaviour
{
    [Header("Interaction & Hotbar")]
    public float interactionRange = 8f;
    [Tooltip("Drag blocks here to populate slots 1-5")]
    public BlockData[] hotbar = new BlockData[5];
    private int currentSlotIndex = 0;

    // Reference to the world
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

    [Header("Collision Settings")]
    public float playerRadius = 0.4f;
    public LayerMask collisionMask;

    // Internal State
    Rigidbody rb;
    float yaw;
    float pitch;
    Vector3 desiredVelocity;
    bool isSprinting;

    void Awake()
    {
        voxelWorld = FindObjectOfType<VoxelWorld>();

        if (externalCamera == null && Camera.main != null)
            externalCamera = Camera.main.transform;

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        // Physics Setup
        rb.useGravity = false;
        rb.isKinematic = true;
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
        HandleHotbar();
        CheckForSuffocation();
    }

    void CheckForSuffocation()
    {
        if (voxelWorld == null) return;
        BlockData currentBlock = voxelWorld.GetBlock(transform.position);

        if (currentBlock != null)
        {
            Vector3 surfacePos = FindSurface(transform.position);
            transform.position = surfacePos;
            desiredVelocity = Vector3.zero;
        }
    }

    Vector3 FindSurface(Vector3 startPos)
    {
        int x = Mathf.FloorToInt(startPos.x);
        int z = Mathf.FloorToInt(startPos.z);

        for (int y = 128; y > 0; y--)
        {
            Vector3 checkPos = new Vector3(x + 0.5f, y, z + 0.5f);
            if (voxelWorld.GetBlock(checkPos) == null)
            {
                Vector3 belowPos = new Vector3(x + 0.5f, y - 1, z + 0.5f);
                if (voxelWorld.GetBlock(belowPos) != null)
                {
                    return new Vector3(startPos.x, y + 1.0f, startPos.z);
                }
            }
        }
        return startPos;
    }

    void HandleHotbar()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) currentSlotIndex = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) currentSlotIndex = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) currentSlotIndex = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) currentSlotIndex = 3;
        if (Input.GetKeyDown(KeyCode.Alpha5)) currentSlotIndex = 4;
        if (currentSlotIndex >= hotbar.Length) currentSlotIndex = 0;
    }

    void HandleInteraction()
    {
        if (voxelWorld == null || externalCamera == null) return;

        Ray ray = new Ray(externalCamera.position, externalCamera.forward);

        if (Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(ray, out RaycastHit hit, interactionRange))
            {
                Vector3 targetPos = hit.point - (hit.normal * 0.1f);
                voxelWorld.ModifyBlock(targetPos, null);
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            BlockData blockToPlace = (hotbar != null && currentSlotIndex < hotbar.Length) ? hotbar[currentSlotIndex] : null;
            if (blockToPlace != null && Physics.Raycast(ray, out RaycastHit hit, interactionRange))
            {
                Vector3 targetPos = hit.point + (hit.normal * 0.1f);
                voxelWorld.ModifyBlock(targetPos, blockToPlace);
            }
        }
    }

    void HandleInput()
    {
        // Look (Mouse)
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y") * (invertY ? 1f : -1f);

        yaw += mx * mouseSensitivity;
        pitch += my * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Movement (Keyboard)
        // FIX: Used GetAxisRaw to prevent floaty/sliding stop
        float forwardInput = Input.GetAxisRaw("Vertical");
        float strafeInput = Input.GetAxisRaw("Horizontal");

        float up = 0f;
        if (Input.GetKey(KeyCode.Space)) up += 1f;
        bool descend = Input.GetKey(KeyCode.CapsLock) || Input.GetKey(KeyCode.LeftControl);
        if (descend) up -= 1f;

        isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float targetSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        Vector3 camForward = externalCamera.forward;
        Vector3 camRight = externalCamera.right;

        if (isSprinting)
        {
            // Free Flight
            Vector3 flightMove = (camForward * forwardInput + camRight * strafeInput);
            // Normalize so diagonal isn't faster
            if (flightMove.sqrMagnitude > 1f) flightMove.Normalize();
            desiredVelocity = flightMove * targetSpeed;
        }
        else
        {
            // Planar Hover
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 horizontalMove = (camForward * forwardInput + camRight * strafeInput);
            // Normalize so diagonal isn't faster
            if (horizontalMove.sqrMagnitude > 1f) horizontalMove.Normalize();

            Vector3 horizVelocity = horizontalMove * targetSpeed;
            Vector3 verticalVelocity = Vector3.up * (up * verticalSpeed);

            desiredVelocity = horizVelocity + verticalVelocity;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void FixedUpdate()
    {
        // --- PREDICTIVE COLLISION LOGIC ---

        Vector3 displacement = desiredVelocity * Time.fixedDeltaTime;
        Vector3 finalPos = rb.position;

        if (displacement.magnitude > 0.001f)
        {
            // Cast a sphere forward to see if we hit anything
            if (Physics.SphereCast(rb.position, playerRadius, displacement.normalized, out RaycastHit hit, displacement.magnitude, collisionMask))
            {
                // Hit a wall - stop exactly at the wall surface
                float distanceToMove = Mathf.Max(0, hit.distance - 0.01f);
                finalPos = rb.position + (displacement.normalized * distanceToMove);
            }
            else
            {
                // Path is clear
                finalPos = rb.position + displacement;
            }
        }

        rb.MovePosition(finalPos);

        // Rotation
        Quaternion rigRot = Quaternion.Euler(0f, yaw, 0f);
        rb.MoveRotation(rigRot);

        // Camera Follow
        if (externalCamera != null)
        {
            Vector3 worldCamPos = rb.position + rigRot * cameraLocalOffset;
            externalCamera.position = worldCamPos;
            Quaternion camLocalPitch = Quaternion.Euler(pitch, 0f, 0f);
            externalCamera.rotation = rigRot * camLocalPitch;
        }
    }

    void OnGUI()
    {
        string blockName = "None";
        if (hotbar != null && currentSlotIndex < hotbar.Length && hotbar[currentSlotIndex] != null)
        {
            blockName = hotbar[currentSlotIndex].blockName;
        }

        GUI.Label(new Rect(20, 20, 300, 50), $"Slot {currentSlotIndex + 1}: {blockName}");
        if (isSprinting) GUI.Label(new Rect(20, 40, 300, 50), ">> TURBO <<");
    }
}