using UnityEngine;
using System.Collections.Generic;

public class CreativeInventory : MonoBehaviour
{
    [Header("References")]
    public GameModeController gameController;
    public int iconSize = 40;
    public int padding = 2;

    private bool showInventory = false;
    private Vector2 scrollPosition;

    // 0 = Standard, 1 = Color Palette
    private int currentTab = 0;

    private List<BlockData> standardBlocks = new List<BlockData>();
    private List<BlockData> colorBlocks = new List<BlockData>();

    // --- VISUALS ---
    private Texture2D solidTexture;
    private GUIStyle flatButtonStyle;

    void Start()
    {
        if (gameController == null) gameController = FindObjectOfType<GameModeController>();

        // 1. Create a 1x1 White Texture for solid rendering
        solidTexture = new Texture2D(1, 1);
        solidTexture.SetPixel(0, 0, Color.white);
        solidTexture.Apply();

        RefreshLists();
    }

    // Helper to create the style once (Performance)
    void InitStyles()
    {
        if (flatButtonStyle == null)
        {
            flatButtonStyle = new GUIStyle(GUI.skin.button);
            flatButtonStyle.normal.background = solidTexture; // Remove default grey gradient
            flatButtonStyle.active.background = solidTexture;
            flatButtonStyle.hover.background = solidTexture;
            flatButtonStyle.border = new RectOffset(1, 1, 1, 1); // Slight border definition
            flatButtonStyle.margin = new RectOffset(padding / 2, padding / 2, padding / 2, padding / 2);
        }
    }

    void RefreshLists()
    {
        standardBlocks.Clear();
        colorBlocks.Clear();

        if (BlockManager.Instance != null)
        {
            if (BlockManager.Instance.colorPalette != null)
                colorBlocks.AddRange(BlockManager.Instance.colorPalette);

            foreach (var b in BlockManager.Instance.loadedBlocks)
            {
                if (!colorBlocks.Contains(b))
                    standardBlocks.Add(b);
            }
        }
    }

    void Update()
    {
        if (showInventory && !(gameController.ActiveMode is Mode_Builder))
            SetInventoryState(false);

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (gameController.ActiveMode is Mode_Builder)
                SetInventoryState(!showInventory);
        }
    }

    void SetInventoryState(bool isOpen)
    {
        showInventory = isOpen;
        Cursor.visible = showInventory;
        Cursor.lockState = showInventory ? CursorLockMode.None : CursorLockMode.Locked;
        if (gameController != null && gameController.movement != null)
            gameController.movement.InputLocked = showInventory;

        if (isOpen) RefreshLists();
    }

    void OnGUI()
    {
        if (!showInventory) return;
        InitStyles(); // Ensure styles are ready

        Rect windowRect = new Rect(50, 50, Screen.width - 100, Screen.height - 100);

        // --- 1. OPAQUE BACKGROUND ---
        // Draw a solid dark box behind the UI to prevent world bleed-through
        Color oldColor = GUI.color;
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 1.0f); // Solid Dark Grey
        GUI.DrawTexture(windowRect, solidTexture);
        GUI.color = oldColor; // Reset

        // Draw the standard Frame on top
        GUI.Box(windowRect, "Creative Library");

        // --- TABS ---
        GUILayout.BeginArea(new Rect(windowRect.x + 20, windowRect.y + 30, windowRect.width - 40, 40));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Standard Blocks", GUILayout.Height(30))) currentTab = 0;
        if (GUILayout.Button($"Color Palette ({colorBlocks.Count})", GUILayout.Height(30))) currentTab = 1;
        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        // --- SCROLL VIEW ---
        Rect scrollRect = new Rect(windowRect.x + 20, windowRect.y + 80, windowRect.width - 40, windowRect.height - 140);

        if (currentTab == 0) DrawStandardGrid(scrollRect);
        else DrawColorGrid(scrollRect);

        // --- HOTBAR ---
        DrawHotbarPreview();
    }

    void DrawStandardGrid(Rect area)
    {
        GUILayout.BeginArea(area);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        int columns = Mathf.FloorToInt((area.width - 20) / (iconSize + padding));
        if (columns < 1) columns = 1;

        int index = 0;
        while (index < standardBlocks.Count)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < columns; i++)
            {
                if (index >= standardBlocks.Count) break;
                DrawBlockButton(standardBlocks[index], true); // Standard Button Style
                index++;
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawColorGrid(Rect area)
    {
        GUILayout.BeginArea(area);

        // Calculate exact content width for 32 columns
        // Note: We use 'iconSize' directly here because custom style handles margins internally
        float contentWidth = 32 * (iconSize + padding);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.Width(area.width), GUILayout.Height(area.height));

        GUILayout.BeginVertical(GUILayout.Width(contentWidth));

        int index = 0;
        while (index < colorBlocks.Count)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 32; i++)
            {
                if (index >= colorBlocks.Count)
                {
                    GUILayout.Space(iconSize + padding);
                }
                else
                {
                    DrawBlockButton(colorBlocks[index], false); // Flat Button Style
                    index++;
                }
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawBlockButton(BlockData b, bool isStandard)
    {
        string btnText = isStandard ? b.blockName : "";
        Color originalBg = GUI.backgroundColor;

        if (!isStandard)
        {
            // Set the background color for our white texture
            GUI.backgroundColor = b.blockColor;

            // Check Hover for Tooltip
            if (new Rect(GUILayoutUtility.GetLastRect()).Contains(Event.current.mousePosition))
            {
                GUI.Label(new Rect(Event.current.mousePosition.x + 15, Event.current.mousePosition.y, 100, 20), b.blockName);
            }
        }

        // --- BUTTON DRAWING ---
        // If Standard: Use default skin. If Color: Use our Flat Opaque Style.
        bool clicked = false;
        if (isStandard)
            clicked = GUILayout.Button(btnText, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
        else
            clicked = GUILayout.Button(btnText, flatButtonStyle, GUILayout.Width(iconSize), GUILayout.Height(iconSize));

        if (clicked)
        {
            gameController.sharedHotbar[gameController.currentSlotIndex] = b;
        }

        GUI.backgroundColor = originalBg;

        // Hotkey 1-5
        Rect btnRect = GUILayoutUtility.GetLastRect();
        if (btnRect.Contains(Event.current.mousePosition) && Event.current.type == EventType.KeyDown)
        {
            KeyCode k = Event.current.keyCode;
            if (k >= KeyCode.Alpha1 && k <= KeyCode.Alpha5)
            {
                gameController.sharedHotbar[k - KeyCode.Alpha1] = b;
                Event.current.Use();
            }
        }
    }

    void DrawHotbarPreview()
    {
        int barWidth = 300;
        int startX = (Screen.width - barWidth) / 2;
        int y = Screen.height - 60;
        for (int i = 0; i < 5; i++)
        {
            BlockData b = gameController.sharedHotbar[i];
            string name = (b != null) ? b.blockName : "Empty";

            if (i == gameController.currentSlotIndex) GUI.color = Color.yellow;
            else GUI.color = Color.white;

            if (GUI.Button(new Rect(startX + (i * 60), y, 50, 50), $"{i + 1}\n{name}"))
                gameController.currentSlotIndex = i;
        }
        GUI.color = Color.white;
    }
}