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

    private int currentTab = 0;
    private List<BlockData> standardBlocks = new List<BlockData>();
    private List<BlockData> colorBlocks = new List<BlockData>();

    private Texture2D solidTexture;
    private GUIStyle flatButtonStyle;

    void Start()
    {
        if (gameController == null) gameController = FindObjectOfType<GameModeController>();

        solidTexture = new Texture2D(1, 1);
        solidTexture.SetPixel(0, 0, Color.white);
        solidTexture.Apply();

        RefreshLists();
    }

    void InitStyles()
    {
        if (flatButtonStyle == null)
        {
            flatButtonStyle = new GUIStyle(GUI.skin.button);
            flatButtonStyle.normal.background = solidTexture;
            flatButtonStyle.active.background = solidTexture;
            flatButtonStyle.hover.background = solidTexture;
            flatButtonStyle.border = new RectOffset(1, 1, 1, 1);
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
                if (!colorBlocks.Contains(b)) standardBlocks.Add(b);
            }
        }
    }

    void Update()
    {
        // --- NEW: Block input if command console is open ---
        if (CommandConsole.IsOpen) return;

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
        InitStyles();

        Rect windowRect = new Rect(50, 50, Screen.width - 100, Screen.height - 100);

        Color oldColor = GUI.color;
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 1.0f);
        GUI.DrawTexture(windowRect, solidTexture);
        GUI.color = oldColor;

        GUI.Box(windowRect, "Creative Library");

        GUILayout.BeginArea(new Rect(windowRect.x + 20, windowRect.y + 30, windowRect.width - 40, 40));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Standard Blocks", GUILayout.Height(30))) currentTab = 0;
        if (GUILayout.Button($"Color Palette ({colorBlocks.Count})", GUILayout.Height(30))) currentTab = 1;
        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        Rect scrollRect = new Rect(windowRect.x + 20, windowRect.y + 80, windowRect.width - 40, windowRect.height - 140);

        if (currentTab == 0) DrawStandardGrid(scrollRect);
        else DrawColorGrid(scrollRect);

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
                DrawBlockButton(standardBlocks[index], true);
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
        float contentWidth = 32 * (iconSize + padding);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.Width(area.width), GUILayout.Height(area.height));
        GUILayout.BeginVertical(GUILayout.Width(contentWidth));

        int index = 0;
        while (index < colorBlocks.Count)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 32; i++)
            {
                if (index >= colorBlocks.Count) GUILayout.Space(iconSize + padding);
                else
                {
                    DrawBlockButton(colorBlocks[index], false);
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
            GUI.backgroundColor = b.blockColor;
            if (new Rect(GUILayoutUtility.GetLastRect()).Contains(Event.current.mousePosition))
            {
                GUI.Label(new Rect(Event.current.mousePosition.x + 15, Event.current.mousePosition.y, 100, 20), b.blockName);
            }
        }

        bool clicked = isStandard ? GUILayout.Button(btnText, GUILayout.Width(iconSize), GUILayout.Height(iconSize)) : GUILayout.Button(btnText, flatButtonStyle, GUILayout.Width(iconSize), GUILayout.Height(iconSize));

        if (clicked)
        {
            int hotbarIndex = 27 + gameController.currentSlotIndex;
            gameController.inventory[hotbarIndex].block = b;
            gameController.inventory[hotbarIndex].count = b.maxToolUses > 0 ? 1 : 64;
            gameController.inventory[hotbarIndex].durability = b.maxToolUses > 0 ? b.maxToolUses : 0;
        }

        GUI.backgroundColor = originalBg;

        Rect btnRect = GUILayoutUtility.GetLastRect();
        if (btnRect.Contains(Event.current.mousePosition) && Event.current.type == EventType.KeyDown)
        {
            KeyCode k = Event.current.keyCode;
            if (k >= KeyCode.Alpha1 && k <= KeyCode.Alpha9)
            {
                int targetHotbarIndex = 27 + (k - KeyCode.Alpha1);
                gameController.inventory[targetHotbarIndex].block = b;
                gameController.inventory[targetHotbarIndex].count = b.maxToolUses > 0 ? 1 : 64;
                gameController.inventory[targetHotbarIndex].durability = b.maxToolUses > 0 ? b.maxToolUses : 0;
                Event.current.Use();
            }
        }
    }

    void DrawHotbarPreview()
    {
        int barWidth = 9 * 50 + 8 * 10;
        int startX = (Screen.width - barWidth) / 2;
        int y = Screen.height - 60;

        for (int i = 0; i < 9; i++)
        {
            BlockData b = gameController.inventory[27 + i].block;
            string name = (b != null) ? (b.blockName.Length > 6 ? b.blockName.Substring(0, 5) + "." : b.blockName) : "Empty";

            if (i == gameController.currentSlotIndex) GUI.color = Color.yellow;
            else GUI.color = Color.white;

            if (GUI.Button(new Rect(startX + (i * 60), y, 50, 50), $"{i + 1}\n{name}"))
                gameController.currentSlotIndex = i;
        }
        GUI.color = Color.white;
    }
}