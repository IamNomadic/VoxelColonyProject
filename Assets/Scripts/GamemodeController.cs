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
    public IGameMode ActiveMode => activeMode; // Public property for Inventory check

    // UI State
    private bool showMenu = false;
    public bool IsInputLocked => showMenu;

    // Shared Data
    public BlockData[] sharedHotbar = new BlockData[5];
    public int currentSlotIndex = 0;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        world = FindObjectOfType<VoxelWorld>();
        mainCamera = movement.externalCamera != null ? movement.externalCamera.GetComponent<Camera>() : Camera.main;

        if (interactionMask == 0) interactionMask = -1;

        modes.Add(new Mode_Builder(this));
        modes.Add(new Mode_Commander(this));
        modes.Add(new Mode_Scanner(this));

        SwitchMode(modes[0]);
    }

    void Update()
    {
        // 1. Shared Hotbar Input
        if (Input.GetKeyDown(KeyCode.Alpha1)) currentSlotIndex = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) currentSlotIndex = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) currentSlotIndex = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) currentSlotIndex = 3;
        if (Input.GetKeyDown(KeyCode.Alpha5)) currentSlotIndex = 4;

        // 2. Game Mode Menu (CHANGED to BackQuote ` )
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            showMenu = !showMenu;
            Cursor.visible = showMenu;
            Cursor.lockState = showMenu ? CursorLockMode.None : CursorLockMode.Locked;
            movement.InputLocked = showMenu;
        }

        if (showMenu) return;

        // 3. Process Active Mode
        if (activeMode != null)
        {
            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            activeMode.OnUpdate(ray);
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