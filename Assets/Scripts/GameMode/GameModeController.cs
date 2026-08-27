using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class InventorySlot
{
    public BlockData block;
    public int count;
    public int durability;

    public int MaxStack => (block != null && block.maxToolUses > 0) ? 1 : 64;
    public bool IsEmpty => block == null || count <= 0;

    public void Clear()
    {
        block = null; count = 0; durability = 0;
    }

    public InventorySlot Clone()
    {
        return new InventorySlot { block = this.block, count = this.count, durability = this.durability };
    }
}

[RequireComponent(typeof(PlayerMovement))]
public class GameModeController : MonoBehaviour
{
    [Header("Global Settings")]
    public float interactionRange = 100f;
    public LayerMask interactionMask;

    [Header("Hand Model Settings")]
    public Vector3 handOffset = new Vector3(0.4f, -0.4f, 0.7f);

    // --- DYNAMIC INVENTORY & STATS ---
    [SerializeField] private InventorySlot[] ghostInventory = new InventorySlot[36];
    public InventorySlot[] inventory => PossessedPawn != null ? PossessedPawn.inventory : ghostInventory;

    [SerializeField] private PlayerStats ghostStats;
    public PlayerStats stats => PossessedPawn != null ? PossessedPawn.stats : ghostStats;

    [HideInInspector] public VoxelWorld world;
    [HideInInspector] public PlayerMovement movement;
    [HideInInspector] public Camera mainCamera;

    private List<IGameMode> modes = new List<IGameMode>();
    private IGameMode activeMode;
    public IGameMode ActiveMode => activeMode;

    private bool showMenu = false;
    public bool IsInputLocked => showMenu;

    public BlockData[] sharedHotbar = new BlockData[9];
    public int currentSlotIndex = 0;

    private GameObject highlightCursor;
    private MeshRenderer highlightRenderer;
    private GameObject heldItemModel;
    private BlockData currentHeldBlock;
    private float swingIntensity = 0f;

    public Pawn PossessedPawn { get; private set; }

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        world = FindObjectOfType<VoxelWorld>();
        mainCamera = movement.externalCamera != null ? movement.externalCamera.GetComponent<Camera>() : Camera.main;

        if (interactionMask == 0) interactionMask = -1;

        // Initialize Ghost Inventory
        for (int i = 0; i < 36; i++)
        {
            if (ghostInventory[i] == null) ghostInventory[i] = new InventorySlot();
        }

        if (sharedHotbar == null || sharedHotbar.Length != 9) sharedHotbar = new BlockData[9];

        modes.Add(new Mode_Survival(this));
        modes.Add(new Mode_Builder(this));
        modes.Add(new Mode_Commander(this));
        modes.Add(new Mode_Scanner(this));

        SwitchMode(modes[0]);
        CreateHighlightCursor();
    }

    void Update()
    {
        // Block input if command console is open
        if (CommandConsole.IsOpen) return;

        // Eject if the pawn we are controlling dies!
        if (PossessedPawn != null && stats != null && stats.dead)
        {
            UnpossessPawn();
        }

        // Sync visual hotbar to whatever inventory is currently active
        for (int i = 0; i < 9; i++) sharedHotbar[i] = inventory[27 + i].block;

        if (Input.GetKeyDown(KeyCode.Alpha1)) currentSlotIndex = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) currentSlotIndex = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) currentSlotIndex = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) currentSlotIndex = 3;
        if (Input.GetKeyDown(KeyCode.Alpha5)) currentSlotIndex = 4;
        if (Input.GetKeyDown(KeyCode.Alpha6)) currentSlotIndex = 5;
        if (Input.GetKeyDown(KeyCode.Alpha7)) currentSlotIndex = 6;
        if (Input.GetKeyDown(KeyCode.Alpha8)) currentSlotIndex = 7;
        if (Input.GetKeyDown(KeyCode.Alpha9)) currentSlotIndex = 8;

        if (!showMenu)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                if (scroll > 0f) currentSlotIndex--;
                else if (scroll < 0f) currentSlotIndex++;

                if (currentSlotIndex < 0) currentSlotIndex = 8;
                if (currentSlotIndex > 8) currentSlotIndex = 0;
            }
        }

        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            showMenu = !showMenu;
            Cursor.visible = showMenu;
            Cursor.lockState = showMenu ? CursorLockMode.None : CursorLockMode.Locked;
            movement.InputLocked = showMenu;
        }

        UpdateHighlightPosition();
        UpdateHandModel();

        if (showMenu) return;

        if (activeMode != null)
        {
            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            activeMode.OnUpdate(ray);
        }
    }

    public void PossessPawn(Pawn targetPawn)
    {
        PossessedPawn = targetPawn;

        CharacterController cc = movement.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.position = targetPawn.transform.position;
        Physics.SyncTransforms();

        if (cc != null) cc.enabled = true;

        targetPawn.Possess();
        SwitchMode(modes[0]);
    }

    public void UnpossessPawn()
    {
        if (PossessedPawn == null) return;

        CharacterController cc = movement.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        PossessedPawn.Unpossess(transform.position);
        PossessedPawn = null;

        transform.position += Vector3.up * 2f;
        Physics.SyncTransforms();

        if (cc != null) cc.enabled = true;

        SwitchMode(modes[2]);
    }

    public void SwitchMode(IGameMode newMode)
    {
        if (activeMode != null) activeMode.OnExit();
        activeMode = newMode;
        activeMode.OnEnter();
        activeMode.SetupMovement(movement);
    }

    void UpdateHandModel()
    {
        if (mainCamera == null) return;

        BlockData expectedBlock = inventory[27 + currentSlotIndex].IsEmpty ? null : inventory[27 + currentSlotIndex].block;

        if (expectedBlock != currentHeldBlock)
        {
            currentHeldBlock = expectedBlock;

            if (heldItemModel != null) Destroy(heldItemModel);

            if (currentHeldBlock != null)
            {
                heldItemModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(heldItemModel.GetComponent<Collider>());

                Transform t = heldItemModel.transform;
                t.SetParent(mainCamera.transform, false);
                heldItemModel.layer = 2;

                MeshFilter mf = heldItemModel.GetComponent<MeshFilter>();
                if (mf != null && mf.mesh != null)
                {
                    Mesh m = mf.mesh;

                    // --- 1. Vertex Colors ---
                    Color[] colors = new Color[m.vertices.Length];
                    Color c = currentHeldBlock.tintWithBlockColor ? currentHeldBlock.blockColor : Color.white;
                    if (c.a == 0) c.a = 1f;
                    for (int i = 0; i < colors.Length; i++) colors[i] = c;
                    m.colors = colors;

                    // --- 2. UV Mapping (Fixes the Atlas Issue) ---
                    Vector2[] uvs = m.uv;
                    Vector3[] normals = m.normals;
                    float uvSz = BlockManager.Instance != null ? 1f / BlockManager.Instance.atlasGridSize : 1f;

                    for (int i = 0; i < uvs.Length; i++)
                    {
                        Vector3 norm = new Vector3(Mathf.Round(normals[i].x), Mathf.Round(normals[i].y), Mathf.Round(normals[i].z));
                        Vector2 gridUV = currentHeldBlock.GetUV(norm);
                        Vector2 atlasUV = new Vector2(gridUV.x * uvSz, gridUV.y * uvSz);

                        float localU = uvs[i].x;
                        float localV = uvs[i].y;

                        // Maintain support for slabs/custom bounds if placing non-standard blocks
                        if (Mathf.Abs(norm.y) > 0.5f) { localU = Mathf.Lerp(currentHeldBlock.boundsMin.x, currentHeldBlock.boundsMax.x, uvs[i].x); localV = Mathf.Lerp(currentHeldBlock.boundsMin.z, currentHeldBlock.boundsMax.z, uvs[i].y); }
                        else if (Mathf.Abs(norm.z) > 0.5f) { localU = Mathf.Lerp(currentHeldBlock.boundsMin.x, currentHeldBlock.boundsMax.x, uvs[i].x); localV = Mathf.Lerp(currentHeldBlock.boundsMin.y, currentHeldBlock.boundsMax.y, uvs[i].y); }
                        else { localU = Mathf.Lerp(currentHeldBlock.boundsMin.z, currentHeldBlock.boundsMax.z, uvs[i].x); localV = Mathf.Lerp(currentHeldBlock.boundsMin.y, currentHeldBlock.boundsMax.y, uvs[i].y); }

                        uvs[i] = new Vector2(atlasUV.x + (localU * uvSz), atlasUV.y + (localV * uvSz));
                    }
                    m.uv = uvs;
                }

                MeshRenderer rend = heldItemModel.GetComponent<MeshRenderer>();
                Material mat;

                if (currentHeldBlock.blockMaterial != null)
                {
                    mat = new Material(currentHeldBlock.blockMaterial);
                }
                else if (currentHeldBlock.isTransparent && BlockManager.Instance != null && BlockManager.Instance.transparentMaterial != null)
                {
                    mat = new Material(BlockManager.Instance.transparentMaterial);
                }
                else
                {
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                    mat = urpShader != null ? new Material(urpShader) : new Material(Shader.Find("Standard"));
                }

                Color matColor = currentHeldBlock.tintWithBlockColor ? currentHeldBlock.blockColor : Color.white;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", matColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", matColor);

                rend.material = mat;
            }
        }

        if (heldItemModel != null)
        {
            bool isInteracting = (Input.GetMouseButton(0) || Input.GetMouseButton(1)) && !IsInputLocked;
            swingIntensity = Mathf.Lerp(swingIntensity, isInteracting ? 1f : 0f, Time.deltaTime * 15f);

            float swingAnim = Mathf.Sin(Time.time * 15f) * swingIntensity;
            float breathe = Mathf.Sin(Time.time * 2f) * 0.02f;

            heldItemModel.transform.localPosition = handOffset + new Vector3(
                0f,
                breathe - Mathf.Abs(swingAnim) * 0.15f,
                swingAnim * 0.3f
            );

            bool isTool = currentHeldBlock.maxToolUses > 0;

            if (isTool)
            {
                // Flat plane rendering for tools
                heldItemModel.transform.localRotation = Quaternion.Euler((swingAnim * -70f) + 15f, -45f, 0f);
                // Squished to 0.02f to simulate a flat, thin 2D sprite
                heldItemModel.transform.localScale = new Vector3(0.4f, 0.4f, 0.02f);
            }
            else
            {
                // Normal 3D Block rendering
                heldItemModel.transform.localRotation = Quaternion.Euler(swingAnim * -50f, -20f, 0f);
                heldItemModel.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            }
        }
    }

    public void AddItem(BlockData block, int amount, int incomingDurability = 0)
    {
        if (block == null) return;

        for (int i = 35; i >= 0; i--)
        {
            if (!inventory[i].IsEmpty && inventory[i].block == block && inventory[i].count < inventory[i].MaxStack)
            {
                int space = inventory[i].MaxStack - inventory[i].count;
                int transfer = Mathf.Min(space, amount);
                inventory[i].count += transfer;
                amount -= transfer;
                if (amount <= 0) return;
            }
        }

        for (int i = 27; i < 36; i++)
        {
            if (inventory[i].IsEmpty)
            {
                int transfer = Mathf.Min(block.maxToolUses > 0 ? 1 : 64, amount);
                inventory[i].block = block;
                inventory[i].count = transfer;
                inventory[i].durability = incomingDurability > 0 ? incomingDurability : block.maxToolUses;
                amount -= transfer;
                if (amount <= 0) return;
            }
        }
        for (int i = 0; i < 27; i++)
        {
            if (inventory[i].IsEmpty)
            {
                int transfer = Mathf.Min(block.maxToolUses > 0 ? 1 : 64, amount);
                inventory[i].block = block;
                inventory[i].count = transfer;
                inventory[i].durability = incomingDurability > 0 ? incomingDurability : block.maxToolUses;
                amount -= transfer;
                if (amount <= 0) return;
            }
        }
    }

    void CreateHighlightCursor()
    {
        highlightCursor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        highlightCursor.name = "BlockHighlight";
        Destroy(highlightCursor.GetComponent<Collider>());
        highlightRenderer = highlightCursor.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(1f, 1f, 1f, 0.4f);
        highlightRenderer.material = mat;
        highlightCursor.transform.localScale = Vector3.one * 1.01f;
    }

    void UpdateHighlightPosition()
    {
        if (highlightCursor == null) return;
        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactionMask))
        {
            Vector3 target = hit.point - (hit.normal * 0.05f);
            highlightCursor.transform.position = new Vector3(Mathf.FloorToInt(target.x) + 0.5f, Mathf.FloorToInt(target.y) + 0.5f, Mathf.FloorToInt(target.z) + 0.5f);
            highlightCursor.transform.rotation = Quaternion.identity;
            if (!highlightCursor.activeSelf) highlightCursor.SetActive(true);
        }
        else if (highlightCursor.activeSelf) highlightCursor.SetActive(false);
    }

    void OnGUI()
    {
        if (showMenu)
        {
            GUI.Box(new Rect(10, 10, 160, 20 + (modes.Count * 40)), "SELECT MODE");
            for (int i = 0; i < modes.Count; i++)
            {
                if (GUI.Button(new Rect(20, 35 + (i * 40), 140, 30), modes[i].ModeName))
                {
                    SwitchMode(modes[i]);
                    showMenu = false;
                    movement.InputLocked = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }
        if (!showMenu && activeMode != null) activeMode.OnGUI();
    }
}