using UnityEngine;
using System.Collections.Generic;

public class CreativeInventory : MonoBehaviour
{
    [Header("Settings")]
    public float iconSize = 100f;
    public float padding = 10f;

    // --- INTERNAL DATA ---
    private BlockData[] allBlocks;
    private bool isMenuOpen = false;
    private ExternalCameraFlightRig_CustomControls_Updated flightRig;

    void Start()
    {
        // 1. Auto-Load all blocks from "Assets/Resources/Blocks"
        allBlocks = Resources.LoadAll<BlockData>("Blocks");
        Debug.Log($"[CreativeInventory] Loaded {allBlocks.Length} blocks from Resources/Blocks.");

        // 2. Find the Flight Rig to communicate with it
        flightRig = GetComponent<ExternalCameraFlightRig_CustomControls_Updated>();
    }

    void Update()
    {
        // Toggle Menu with TAB
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isMenuOpen = !isMenuOpen;
            UpdateCursorState();
        }
    }

    void UpdateCursorState()
    {
        if (isMenuOpen)
        {
            // Unlock cursor for menu use
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Disable camera movement while menu is open
            if (flightRig != null) flightRig.isInputLocked = true;
        }
        else
        {
            // Lock cursor back to game
            if (flightRig != null && flightRig.lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // Re-enable camera movement
            if (flightRig != null) flightRig.isInputLocked = false;
        }
    }

    void OnGUI()
    {
        if (!isMenuOpen) return;

        // Draw a dark background
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

        // --- GRID LAYOUT ---
        float x = padding;
        float y = padding;
        float width = Screen.width - (padding * 2);

        GUI.Label(new Rect(x, y, width, 30), "<b>CREATIVE MENU</b> (Hover + Press 1-5 to Assign)");
        y += 40;

        foreach (var block in allBlocks)
        {
            if (block == null) continue;

            Rect btnRect = new Rect(x, y, iconSize, iconSize);

            // Draw Block Box
            GUI.Box(btnRect, block.blockName);

            // Draw Color Preview
            Rect colorRect = new Rect(x + 10, y + 25, iconSize - 20, iconSize - 40);
            Color originalColor = GUI.color;
            GUI.color = block.blockColor;
            GUI.DrawTexture(colorRect, Texture2D.whiteTexture);
            GUI.color = originalColor;

            // --- HOVER LOGIC ---
            if (btnRect.Contains(Event.current.mousePosition))
            {
                // Highlight
                GUI.Box(btnRect, "Selecting...");

                // Detect Number Keys (1-5) to Assign
                if (Event.current.isKey && Event.current.type == EventType.KeyDown)
                {
                    KeyCode key = Event.current.keyCode;
                    if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha5)
                    {
                        int slotIndex = key - KeyCode.Alpha1; // '1' is 49, so 49-49 = 0
                        AssignBlockToHotbar(slotIndex, block);
                    }
                }
            }

            // Grid Math (Move to next column, wrap to next row)
            x += iconSize + padding;
            if (x + iconSize > Screen.width)
            {
                x = padding;
                y += iconSize + padding;
            }
        }
    }

    void AssignBlockToHotbar(int slot, BlockData block)
    {
        if (flightRig != null)
        {
            flightRig.hotbar[slot] = block;
            Debug.Log($"Assigned {block.blockName} to Slot {slot + 1}");
        }
    }
}