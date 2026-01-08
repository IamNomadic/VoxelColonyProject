using UnityEngine;
using System.IO;

[AddComponentMenu("Debug/External Camera Flight Rig - Scanner Mode")]
public class ExternalCameraFlightRig_CustomControls_Updated : MonoBehaviour
{
    [Header("Interaction & Hotbar")]
    public float interactionRange = 100f; // Increased for RTS feel

    // Made public so Inventory can access it
    public BlockData[] hotbar = new BlockData[5];
    public int currentSlotIndex = 0;

    // --- NEW: INPUT LOCK ---
    public bool isInputLocked = false;

    private VoxelWorld voxelWorld;

    [Header("RTS Commander Settings")]
    public LayerMask pawnLayer; // Try to set this to your "Pawn" layer
    public PawnStateMachine selectedPawn;
    public bool commanderMode = false;

    [Header("Structure Scanner")]
    public string saveSubFolder = "SavedStructures";
    private Vector3Int? pointA = null;
    private Vector3Int? pointB = null;

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
        if (isInputLocked)
        {
            desiredVelocity = Vector3.zero;
            return;
        }

        HandleInput();
        HandleInteraction();
        HandleHotbar();
        HandleScanner();
        CheckForSuffocation();
    }

    void HandleInteraction()
    {
        if (voxelWorld == null || externalCamera == null) return;

        // ---------------------------------------------------------
        // 1. ROBUST PAWN SELECTION (Look + E)
        // ---------------------------------------------------------
        if (Input.GetKeyDown(KeyCode.E))
        {
            // Debug: Show where we aimed
            Debug.DrawRay(externalCamera.position, externalCamera.forward * interactionRange, Color.red, 2.0f);

            PawnStateMachine foundPawn = null;

            // STRATEGY A: SphereCast (Forgiving Aim)
            Ray ray = new Ray(externalCamera.position, externalCamera.forward);
            if (Physics.SphereCast(ray, 0.5f, out RaycastHit hit, interactionRange, pawnLayer))
            {
                foundPawn = hit.collider.GetComponent<PawnStateMachine>();
            }

            // STRATEGY B: Fallback (If LayerMask is wrong, search everything)
            if (foundPawn == null)
            {
                RaycastHit[] allHits = Physics.RaycastAll(ray, interactionRange);
                // Sort closest first
                System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var h in allHits)
                {
                    var psm = h.collider.GetComponent<PawnStateMachine>();
                    if (psm != null)
                    {
                        foundPawn = psm;
                        break;
                    }
                }
            }

            // Result
            if (foundPawn != null)
            {
                selectedPawn = foundPawn;
                commanderMode = true;
                Debug.Log($"<color=green>SELECTED: {foundPawn.name}</color>");
            }
            else
            {
                Debug.Log("<color=red>No Pawn Found in sights.</color>");
            }
        }

        bool leftClick = Input.GetMouseButtonDown(0);
        bool rightClick = Input.GetMouseButtonDown(1);

        if (leftClick || rightClick)
        {
            Ray ray = new Ray(externalCamera.position, externalCamera.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, interactionRange, collisionMask);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (hit.collider.gameObject == gameObject) continue;
                if (hit.collider.isTrigger) continue;

                // -----------------------------------------------------
                // 2. COMMANDER MODE (Giving Orders)
                // -----------------------------------------------------
                if (commanderMode && selectedPawn != null)
                {
                    if (rightClick) // COMMAND
                    {
                        Vector3 target = new Vector3(
                            Mathf.Floor(hit.point.x) + 0.5f,
                            Mathf.Floor(hit.point.y) + 1.0f,
                            Mathf.Floor(hit.point.z) + 0.5f
                        );

                        if (Input.GetKey(KeyCode.LeftShift)) selectedPawn.QueueCommand(target);
                        else
                        {
                            selectedPawn.ClearCommands();
                            selectedPawn.QueueCommand(target);
                        }

                        // Visual Confirm
                        Debug.DrawLine(hit.point, hit.point + Vector3.up * 5, Color.green, 2.0f);
                        return; // Stop processing (don't place block)
                    }

                    if (leftClick) // DESELECT
                    {
                        selectedPawn = null;
                        commanderMode = false;
                        Debug.Log("Exiting Commander Mode");
                        return; // Stop processing (don't break block)
                    }
                }

                // -----------------------------------------------------
                // 3. BUILDER MODE (Standard Voxel)
                // -----------------------------------------------------
                if (!commanderMode)
                {
                    if (leftClick)
                    {
                        Vector3 targetPos = hit.point - (hit.normal * 0.1f);
                        voxelWorld.ModifyBlock(targetPos, null);
                    }

                    if (rightClick)
                    {
                        BlockData blockToPlace = (hotbar != null && currentSlotIndex < hotbar.Length) ? hotbar[currentSlotIndex] : null;
                        if (blockToPlace != null)
                        {
                            Vector3 targetPos = hit.point + (hit.normal * 0.1f);
                            voxelWorld.ModifyBlock(targetPos, blockToPlace);
                        }
                    }
                }
                return;
            }
        }
    }

    // ... [Standard Movement Code Below - Unchanged] ...
    void HandleScanner()
    {
        if (pointA.HasValue)
        {
            Vector3 p1 = pointA.Value;
            Vector3 p2 = pointB.HasValue ? pointB.Value : GetLookBlockPos();
            Vector3 min = Vector3.Min(p1, p2);
            Vector3 max = Vector3.Max(p1, p2) + Vector3.one;
            DrawBox(min, max, Color.green);
        }
        if (Input.GetKeyDown(KeyCode.BackQuote)) { pointA = null; pointB = null; Debug.Log("Selection Cleared."); }
        if (Input.GetKeyDown(KeyCode.F))
        {
            Vector3Int hitPos = GetLookBlockPos();
            if (hitPos.y != -999) { if (pointA == null) pointA = hitPos; else pointB = hitPos; }
        }
        if (Input.GetKeyDown(KeyCode.G) && pointA.HasValue && pointB.HasValue) SaveStructure();
    }

    void SaveStructure()
    {
        if (voxelWorld == null) return;
        Vector3Int min = Vector3Int.Min(pointA.Value, pointB.Value);
        Vector3Int max = Vector3Int.Max(pointA.Value, pointB.Value);

        VoxelStructure structData = new VoxelStructure();
        structData.structureName = $"Structure_{System.DateTime.Now:MMdd_HHmm}";

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    BlockData block = voxelWorld.GetBlock(new Vector3(x, y, z));
                    if (block != null) structData.blocks.Add(new VoxelBlockEntry(x - min.x, y - min.y, z - min.z, block.blockName));
                }
            }
        }

        string json = JsonUtility.ToJson(structData, true);
        string folderPath = Path.Combine(Application.dataPath, saveSubFolder);
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, $"{structData.structureName}.json"), json);
        pointA = null; pointB = null;
    }

    Vector3Int GetLookBlockPos()
    {
        Ray ray = new Ray(externalCamera.position, externalCamera.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, interactionRange, collisionMask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.gameObject == gameObject) continue;
            if (hit.collider.isTrigger) continue;
            Vector3 p = hit.point - (hit.normal * 0.1f);
            return new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
        }
        return new Vector3Int(0, -999, 0);
    }

    void DrawBox(Vector3 min, Vector3 max, Color color)
    {
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), color);
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z), color);
        Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), color);
        Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), color);
        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), color);
        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), color);
        Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), color);
        Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), color);
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), color);
        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), color);
        Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), color);
        Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), color);
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
                if (voxelWorld.GetBlock(belowPos) != null) return new Vector3(startPos.x, y + 1.0f, startPos.z);
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

    void HandleInput()
    {
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y") * (invertY ? 1f : -1f);
        yaw += mx * mouseSensitivity;
        pitch += my * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
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
            Vector3 flightMove = (camForward * forwardInput + camRight * strafeInput);
            if (flightMove.sqrMagnitude > 1f) flightMove.Normalize();
            desiredVelocity = flightMove * targetSpeed;
        }
        else
        {
            camForward.y = 0f; camRight.y = 0f; camForward.Normalize(); camRight.Normalize();
            Vector3 horizontalMove = (camForward * forwardInput + camRight * strafeInput);
            if (horizontalMove.sqrMagnitude > 1f) horizontalMove.Normalize();
            desiredVelocity = (horizontalMove * targetSpeed) + (Vector3.up * (up * verticalSpeed));
        }
        if (Input.GetKeyDown(KeyCode.Escape)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    void FixedUpdate()
    {
        Vector3 displacement = desiredVelocity * Time.fixedDeltaTime;
        Vector3 finalPos = rb.position;
        if (displacement.magnitude > 0.001f)
        {
            if (Physics.SphereCast(rb.position, playerRadius, displacement.normalized, out RaycastHit hit, displacement.magnitude, collisionMask))
            {
                float distanceToMove = Mathf.Max(0, hit.distance - 0.01f);
                finalPos = rb.position + (displacement.normalized * distanceToMove);
            }
            else { finalPos = rb.position + displacement; }
        }
        rb.MovePosition(finalPos);
        Quaternion rigRot = Quaternion.Euler(0f, yaw, 0f);
        rb.MoveRotation(rigRot);
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
        if (isInputLocked) return;
        string blockName = "None";
        if (hotbar != null && currentSlotIndex < hotbar.Length && hotbar[currentSlotIndex] != null) blockName = hotbar[currentSlotIndex].blockName;

        string mode = "Flight";
        if (commanderMode) mode = $"<color=yellow>COMMANDING: {(selectedPawn != null ? selectedPawn.name : "None")}</color>";
        else if (pointA.HasValue) mode = "Selecting...";
        else if (pointA.HasValue && pointB.HasValue) mode = "Ready to Save (G)";

        GUI.Label(new Rect(20, 20, 400, 30), $"Mode: {mode}");
        GUI.Label(new Rect(20, 40, 400, 30), $"Slot {currentSlotIndex + 1}: {blockName} (Press Tab for Menu)");
        if (isSprinting) GUI.Label(new Rect(20, 60, 400, 30), ">> TURBO <<");
        if (commanderMode) GUI.Label(new Rect(20, 80, 400, 30), "R-Click: Move | Shift+R-Click: Queue | L-Click: Exit");
    }

    void OnDrawGizmos()
    {
        if (selectedPawn != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, selectedPawn.transform.position + Vector3.up * 2);
        }
    }
}