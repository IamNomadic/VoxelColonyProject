
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(PlayerMovement))]
public class GameModeController : MonoBehaviour
{
    [Header("Global Settings")]
    public float interactionRange = 100f;
    public LayerMask interactionMask;

    // Dependencies
    [HideInInspector] public VoxelWorld world;
    [HideInInspector] public PlayerMovement movement;
    [HideInInspector] public Camera mainCamera;

    // Modes
    private List<IGameMode> modes = new List<IGameMode>();
    private IGameMode activeMode;
    public IGameMode ActiveMode => activeMode;

    // UI State
    private bool showMenu = false;
    public bool IsInputLocked => showMenu;

    // Shared Data
    public BlockData[] sharedHotbar = new BlockData[5];
    public int currentSlotIndex = 0;

    // --- VISUAL HIGHLIGHT ---
    private GameObject highlightCursor;
    private MeshRenderer highlightRenderer;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        world = FindObjectOfType<VoxelWorld>();
        mainCamera = movement.externalCamera != null ? movement.externalCamera.GetComponent<Camera>() : Camera.main;

        if (interactionMask == 0) interactionMask = -1;

        // --- REGISTER MODES HERE ---
        modes.Add(new Mode_Survival(this)); // Now index 0 (Default)
        modes.Add(new Mode_Builder(this));
        modes.Add(new Mode_Commander(this));
        modes.Add(new Mode_Scanner(this));

        SwitchMode(modes[0]);

        CreateHighlightCursor();
    }

    void Update()
    {
        // 1. Hotbar
        if (Input.GetKeyDown(KeyCode.Alpha1)) currentSlotIndex = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) currentSlotIndex = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) currentSlotIndex = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) currentSlotIndex = 3;
        if (Input.GetKeyDown(KeyCode.Alpha5)) currentSlotIndex = 4;

        // 2. Menu
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            showMenu = !showMenu;
            Cursor.visible = showMenu;
            Cursor.lockState = showMenu ? CursorLockMode.None : CursorLockMode.Locked;
            movement.InputLocked = showMenu;
        }

        // 3. Highlight Logic
        UpdateHighlightPosition();

        if (showMenu) return;

        // 4. Mode Update
        if (activeMode != null)
        {
            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            activeMode.OnUpdate(ray);
        }
    }

    // --- HIGHLIGHT LOGIC ---
    void CreateHighlightCursor()
    {
        highlightCursor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        highlightCursor.name = "BlockHighlight";

        // Remove Collider so it doesn't block rays
        Destroy(highlightCursor.GetComponent<Collider>());

        highlightRenderer = highlightCursor.GetComponent<MeshRenderer>();

        // Create simple transparent material
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(1f, 1f, 1f, 0.4f); // 40% White Tint
        highlightRenderer.material = mat;

        // Scale slightly > 1 to overlay the block without flickering
        highlightCursor.transform.localScale = Vector3.one * 1.01f;
    }

    void UpdateHighlightPosition()
    {
        if (highlightCursor == null) return;

        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactionMask))
        {
            // Move small amount INTO the block (opposite of normal) to hit the voxel itself
            Vector3 target = hit.point - (hit.normal * 0.05f);

            // FloorToInt is critical for Voxel coordinates (0.9 -> 0, 1.1 -> 1)
            int x = Mathf.FloorToInt(target.x);
            int y = Mathf.FloorToInt(target.y);
            int z = Mathf.FloorToInt(target.z);

            // Unity Primitives pivot at CENTER.
            // Your blocks pivot at BOTTOM-LEFT.
            // So we must offset the primitive by +0.5 to align centers.
            highlightCursor.transform.position = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);

            // Lock rotation to world alignment (Never rotate)
            highlightCursor.transform.rotation = Quaternion.identity;

            if (!highlightCursor.activeSelf) highlightCursor.SetActive(true);
        }
        else
        {
            if (highlightCursor.activeSelf) highlightCursor.SetActive(false);
        }
    }

    public void SwitchMode(IGameMode newMode)
    {
        if (activeMode != null) activeMode.OnExit();
        activeMode = newMode;
        activeMode.OnEnter();
        activeMode.SetupMovement(movement);
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

        if (!showMenu && activeMode != null)
        {
            GUI.Label(new Rect(Screen.width - 200, 10, 200, 30), $"MODE: <color=yellow>{activeMode.ModeName}</color>");
            activeMode.OnGUI();
        }
    }
}