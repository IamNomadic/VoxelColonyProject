using UnityEngine;
using System.Collections.Generic;

public class CreativeInventory : MonoBehaviour
{
    [Header("References")]
    public GameModeController gameController;
    public int iconSize = 50;
    public int padding = 10;

    private bool showInventory = false;
    private List<BlockData> allBlocks = new List<BlockData>();
    private Vector2 scrollPosition;

    void Start()
    {
        if (gameController == null) gameController = FindObjectOfType<GameModeController>();

        // Load Blocks
        if (BlockManager.Instance != null)
        {
            // Skip Air (0)
            for (byte i = 1; i < 255; i++)
            {
                BlockData b = BlockManager.Instance.GetBlockData(i);
                if (b != null) allBlocks.Add(b);
                else break;
            }
        }
    }

    void Update()
    {
        // 1. Safety Check: If we switched out of Builder Mode, force close inventory
        if (showInventory && !(gameController.ActiveMode is Mode_Builder))
        {
            SetInventoryState(false);
        }

        // 2. Toggle Input (CHANGED TO TAB)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            // ONLY allow opening if we are in Builder Mode
            if (gameController.ActiveMode is Mode_Builder)
            {
                SetInventoryState(!showInventory);
            }
            else
            {
                Debug.Log("Inventory is only available in Builder Mode.");
            }
        }
    }

    // Helper method to keep code clean
    void SetInventoryState(bool isOpen)
    {
        showInventory = isOpen;

        if (showInventory)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (gameController != null && gameController.movement != null)
                gameController.movement.InputLocked = true;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            if (gameController != null && gameController.movement != null)
                gameController.movement.InputLocked = false;
        }
    }

    void OnGUI()
    {
        if (!showInventory) return;
        if (gameController == null) return;

        // Background
        GUI.Box(new Rect(50, 50, Screen.width - 100, Screen.height - 100), "Block Library (Hover and press 1-5 to assign)");

        // Scroll View
        GUILayout.BeginArea(new Rect(70, 80, Screen.width - 140, Screen.height - 140));
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        // Grid Layout
        int columns = Mathf.FloorToInt((Screen.width - 140) / (iconSize + padding));
        if (columns < 1) columns = 1;

        int index = 0;
        while (index < allBlocks.Count)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < columns; i++)
            {
                if (index >= allBlocks.Count) break;

                BlockData b = allBlocks[index];

                // 1. Draw the Button
                if (GUILayout.Button(b.blockName, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
                {
                    // Standard Click: Assign to CURRENT selected slot
                    int current = gameController.currentSlotIndex;
                    gameController.sharedHotbar[current] = b;
                    Debug.Log($"Assigned {b.blockName} to Selected Slot ({current + 1})");
                }

                // 2. Hover & Hotkey Logic
                Rect btnRect = GUILayoutUtility.GetLastRect();

                if (btnRect.Contains(Event.current.mousePosition))
                {
                    if (Event.current.isKey && Event.current.type == EventType.KeyDown)
                    {
                        if (Event.current.keyCode == KeyCode.Alpha1) AssignToSlot(0, b);
                        else if (Event.current.keyCode == KeyCode.Alpha2) AssignToSlot(1, b);
                        else if (Event.current.keyCode == KeyCode.Alpha3) AssignToSlot(2, b);
                        else if (Event.current.keyCode == KeyCode.Alpha4) AssignToSlot(3, b);
                        else if (Event.current.keyCode == KeyCode.Alpha5) AssignToSlot(4, b);
                    }
                }

                index++;
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        DrawHotbarPreview();
    }

    void AssignToSlot(int slotIndex, BlockData block)
    {
        gameController.sharedHotbar[slotIndex] = block;
        Debug.Log($"Quick-Assigned {block.blockName} to Slot {slotIndex + 1}");
        Event.current.Use();
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

            if (i == gameController.currentSlotIndex)
                GUI.color = Color.yellow;
            else
                GUI.color = Color.white;

            if (GUI.Button(new Rect(startX + (i * 60), y, 50, 50), $"{i + 1}\n{name}"))
            {
                gameController.currentSlotIndex = i;
            }
        }
        GUI.color = Color.white;
    }
}