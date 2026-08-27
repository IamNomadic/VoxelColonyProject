
using UnityEngine;
using System.Collections.Generic;

public class Mode_Survival : IGameMode
{
    private GameModeController ctrl;
    private float interactReach = 6f;
    private float interactCooldown = 0.2f;
    private float lastInteractTime = 0f;

    private Vector3Int? miningTarget = null;
    private float miningProgress = 0f;

    private bool showInventory = false;
    private InventorySlot mouseItem = new InventorySlot();

    private bool isDragging = false;
    private int dragButton = -1;
    private HashSet<int> draggedSlots = new HashSet<int>();
    private int originalMouseItemCount = 0;

    public string ModeName => "Survival";

    public Mode_Survival(GameModeController c) { ctrl = c; }

    public void SetupMovement(PlayerMovement move)
    {
        move.SetFlying(false);
        move.obeysSimulationClock = true;
    }

    public void OnEnter()
    {
        ResetMining();
        showInventory = false;
        mouseItem.Clear();
    }

    public void OnExit()
    {
        ResetMining();
        showInventory = false;
        DropItem(mouseItem.block, mouseItem.count, mouseItem.durability);
        mouseItem.Clear();
    }

    private void ResetMining() { miningTarget = null; miningProgress = 0f; }

    public void OnUpdate(Ray ray)
    {
        if (ctrl.IsInputLocked && showInventory)
        {
            showInventory = false;
            if (!mouseItem.IsEmpty) DropItem(mouseItem.block, mouseItem.count, mouseItem.durability);
            mouseItem.Clear();
            return;
        }

        if (ctrl.PossessedPawn != null && Input.GetKeyDown(KeyCode.F))
        {
            ctrl.UnpossessPawn();
            return;
        }

        // Toggle Inventory
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.E)) ToggleInventory();

        if (showInventory || ctrl.IsInputLocked) return;

        if (SimulationClock.Instance != null && SimulationClock.Instance.GetCurrentSpeed() == SimulationClock.Speed.Paused)
        {
            return;
        }

        // Drop item
        if (Input.GetKeyDown(KeyCode.Q))
        {
            InventorySlot activeSlot = ctrl.inventory[27 + ctrl.currentSlotIndex];
            if (!activeSlot.IsEmpty)
            {
                DropItem(activeSlot.block, 1, activeSlot.durability);
                activeSlot.count--;
                if (activeSlot.count <= 0) activeSlot.Clear();
            }
        }

        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, interactReach, ctrl.interactionMask);

        // --- MINING ---
        if (Input.GetMouseButton(0))
        {
            if (hitSomething)
            {
                Vector3 breakPosFloat = hit.point - (hit.normal * 0.05f);
                Vector3Int breakPos = new Vector3Int(Mathf.FloorToInt(breakPosFloat.x), Mathf.FloorToInt(breakPosFloat.y), Mathf.FloorToInt(breakPosFloat.z));

                if (miningTarget != breakPos) { miningTarget = breakPos; miningProgress = 0f; }

                BlockData targetBlock = ctrl.world.GetBlock(breakPosFloat);
                if (targetBlock != null)
                {
                    if (targetBlock.durability < 0f) miningProgress = 0f;
                    else
                    {
                        float totalMiningSpeed = 1f;
                        InventorySlot activeSlot = ctrl.inventory[27 + ctrl.currentSlotIndex];

                        if (!activeSlot.IsEmpty && activeSlot.block.toolType != ToolType.None)
                        {
                            if (activeSlot.block.toolType == targetBlock.preferredTool)
                                totalMiningSpeed = activeSlot.block.toolSpeedMultiplier;
                        }

                        miningProgress += SimulationClock.Instance.SimulationDeltaTime * totalMiningSpeed;

                        if (miningProgress >= targetBlock.durability)
                        {
                            Vector3 spawnPos = new Vector3(breakPos.x + 0.5f, breakPos.y + 0.5f, breakPos.z + 0.5f);
                            GameObject dropObj = new GameObject("Drop_" + targetBlock.blockName);
                            VoxelItemDrop dropScript = dropObj.AddComponent<VoxelItemDrop>();

                            dropScript.Initialize(targetBlock, spawnPos, 1, -1);

                            ctrl.world.ModifyBlock(breakPosFloat, null);
                            ResetMining();
                            lastInteractTime = SimulationClock.Instance.SimulationTime;

                            if (!activeSlot.IsEmpty && activeSlot.block.maxToolUses > 0)
                            {
                                activeSlot.durability--;
                                if (activeSlot.durability <= 0) activeSlot.Clear();
                            }
                        }
                    }
                }
            }
            else ResetMining();
        }
        else ResetMining();

        // --- PLACING ---
        if (Input.GetMouseButton(1) && SimulationClock.Instance.SimulationTime > lastInteractTime + interactCooldown)
        {
            if (hitSomething)
            {
                InventorySlot activeSlot = ctrl.inventory[27 + ctrl.currentSlotIndex];

                if (!activeSlot.IsEmpty && activeSlot.block.maxToolUses <= 0)
                {
                    Vector3 placePos = hit.point + (hit.normal * 0.05f);
                    Vector3Int targetIntPos = new Vector3Int(Mathf.FloorToInt(placePos.x), Mathf.FloorToInt(placePos.y), Mathf.FloorToInt(placePos.z));
                    Vector3 blockCenter = new Vector3(targetIntPos.x + 0.5f, targetIntPos.y + 0.5f, targetIntPos.z + 0.5f);
                    Bounds blockBounds = new Bounds(blockCenter, new Vector3(0.95f, 0.95f, 0.95f));
                    CharacterController cc = ctrl.movement.GetComponent<CharacterController>();

                    if (cc == null || !cc.bounds.Intersects(blockBounds))
                    {
                        ctrl.world.ModifyBlock(placePos, activeSlot.block);
                        activeSlot.count--;
                        if (activeSlot.count <= 0) activeSlot.Clear();
                        lastInteractTime = SimulationClock.Instance.SimulationTime;
                    }
                }
            }
        }
    }

    private void ToggleInventory()
    {
        showInventory = !showInventory;
        ctrl.movement.InputLocked = showInventory;
        Cursor.visible = showInventory;
        Cursor.lockState = showInventory ? CursorLockMode.None : CursorLockMode.Locked;

        if (!showInventory && !mouseItem.IsEmpty)
        {
            DropItem(mouseItem.block, mouseItem.count, mouseItem.durability);
            mouseItem.Clear();
        }
    }

    private void DropItem(BlockData block, int count, int durability = -1)
    {
        if (block == null || count <= 0) return;
        Vector3 spawnPos = ctrl.mainCamera.transform.position + ctrl.mainCamera.transform.forward * 1.5f;

        GameObject dropObj = new GameObject("Drop_" + block.blockName);
        VoxelItemDrop dropScript = dropObj.AddComponent<VoxelItemDrop>();
        dropScript.Initialize(block, spawnPos, count, durability);

        dropObj.GetComponent<Rigidbody>().AddForce(ctrl.mainCamera.transform.forward * 6f, ForceMode.Impulse);
    }

    public void OnGUI()
    {
        if (ctrl.stats != null)
        {
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(10, 10, 200, 70), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(20, 20, 300, 30), $"<b><color=#ff5555>♥ Health:</color></b> {ctrl.stats.CurrentHealth} / {ctrl.stats.MaxHealth}");
            GUI.Label(new Rect(20, 45, 300, 30), $"<b><color=#ffaa00>🍖 Hunger:</color></b> {ctrl.stats.CurrentHunger} / {ctrl.stats.MaxHunger}");
        }

        if (ctrl.PossessedPawn != null)
        {
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(10, 90, 240, 30), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(20, 95, 230, 25), $"Controlling: <color=cyan>{ctrl.PossessedPawn.name}</color> <b>[F] Eject</b>");
        }

        if (showInventory)
        {
            DrawMinecraftInventory();
            return;
        }

        float slotSize = 48f;
        float spacing = 4f;
        float hotbarWidth = (9 * slotSize) + (8 * spacing);
        float startX = (Screen.width - hotbarWidth) / 2f;
        float startY = Screen.height - slotSize - 20f;

        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);
        GUI.DrawTexture(new Rect(startX - 10, startY - 10, hotbarWidth + 20, slotSize + 20), Texture2D.whiteTexture);
        GUI.color = Color.white;

        for (int i = 0; i < 9; i++)
        {
            Rect r = new Rect(startX + i * (slotSize + spacing), startY, slotSize, slotSize);
            DrawSlot(27 + i, r);
        }

        InventorySlot activeSlot = ctrl.inventory[27 + ctrl.currentSlotIndex];
        string blockName = !activeSlot.IsEmpty ? activeSlot.block.blockName : "";
        GUIStyle centerText = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(startX, startY - 30, hotbarWidth, 30), $"<color=white>{blockName}</color>", centerText);

        if (miningTarget.HasValue && miningProgress > 0f)
        {
            BlockData targetBlock = ctrl.world.GetBlock(new Vector3(miningTarget.Value.x, miningTarget.Value.y, miningTarget.Value.z));
            if (targetBlock != null && targetBlock.durability > 0f)
            {
                float pct = Mathf.Clamp01(miningProgress / targetBlock.durability);
                float xPos = (Screen.width / 2f) - 60f;
                float yPos = (Screen.height / 2f) + 30f;

                GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                GUI.DrawTexture(new Rect(xPos, yPos, 120, 10), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(Color.red, Color.green, pct);
                GUI.DrawTexture(new Rect(xPos, yPos, 120 * pct, 10), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }
    }

    private void DrawMinecraftInventory()
    {
        float slotSize = 48f;
        float spacing = 4f;
        float w = (9 * slotSize) + (8 * spacing) + 40f;
        float h = (4 * slotSize) + (3 * spacing) + 80f;

        float cx = (Screen.width / 2f) - (w / 2f);
        float cy = (Screen.height / 2f) - (h / 2f);

        Rect windowRect = new Rect(cx, cy, w, h);
        GUI.Box(windowRect, "<b>Inventory</b>");

        Event e = Event.current;
        int hoveredSlotIndex = -1;

        for (int i = 0; i < 27; i++)
        {
            int col = i % 9;
            int row = i / 9;
            Rect r = new Rect(cx + 20 + col * (slotSize + spacing), cy + 40 + row * (slotSize + spacing), slotSize, slotSize);
            if (r.Contains(e.mousePosition)) hoveredSlotIndex = i;
            DrawSlot(i, r);
        }

        float hotbarOffset = (w - ((9 * slotSize) + (8 * spacing))) / 2f;
        for (int i = 0; i < 9; i++)
        {
            Rect r = new Rect(cx + hotbarOffset + i * (slotSize + spacing), cy + h - slotSize - 20, slotSize, slotSize);
            if (r.Contains(e.mousePosition)) hoveredSlotIndex = 27 + i;
            DrawSlot(27 + i, r);
        }

        switch (e.type)
        {
            case EventType.MouseDown:
                if (hoveredSlotIndex != -1)
                {
                    if (e.button == 0 && e.clickCount >= 2 && !mouseItem.IsEmpty)
                    {
                        GatherItemsToMouse();
                        isDragging = false;
                        draggedSlots.Clear();
                        e.Use();
                    }
                    else
                    {
                        isDragging = true;
                        dragButton = e.button;
                        draggedSlots.Clear();
                        draggedSlots.Add(hoveredSlotIndex);
                        originalMouseItemCount = mouseItem.count;
                    }
                }
                else if (!windowRect.Contains(e.mousePosition) && !mouseItem.IsEmpty)
                {
                    DropItem(mouseItem.block, mouseItem.count, mouseItem.durability);
                    mouseItem.Clear();
                }
                break;

            case EventType.MouseDrag:
                if (isDragging && hoveredSlotIndex != -1) draggedSlots.Add(hoveredSlotIndex);
                break;

            case EventType.MouseUp:
                if (isDragging)
                {
                    if (draggedSlots.Count == 1) DoClick(hoveredSlotIndex, dragButton);
                    else DoDrag();
                    isDragging = false;
                    draggedSlots.Clear();
                }
                break;

            case EventType.KeyDown:
                if (hoveredSlotIndex != -1 && hoveredSlotIndex < 27 && e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha9)
                {
                    int targetHotbar = 27 + (e.keyCode - KeyCode.Alpha1);
                    InventorySlot temp = ctrl.inventory[hoveredSlotIndex].Clone();

                    ctrl.inventory[hoveredSlotIndex].block = ctrl.inventory[targetHotbar].block;
                    ctrl.inventory[hoveredSlotIndex].count = ctrl.inventory[targetHotbar].count;
                    ctrl.inventory[hoveredSlotIndex].durability = ctrl.inventory[targetHotbar].durability;

                    ctrl.inventory[targetHotbar].block = temp.block;
                    ctrl.inventory[targetHotbar].count = temp.count;
                    ctrl.inventory[targetHotbar].durability = temp.durability;
                    e.Use();
                }
                break;
        }

        if (!mouseItem.IsEmpty)
        {
            Rect mouseRect = new Rect(e.mousePosition.x - slotSize / 2, e.mousePosition.y - slotSize / 2, slotSize, slotSize);
            GUI.color = mouseItem.block.blockColor;
            GUI.DrawTexture(new Rect(mouseRect.x + 8, mouseRect.y + 8, mouseRect.width - 16, mouseRect.height - 16), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (mouseItem.block.maxToolUses > 0)
            {
                float durPct = (float)mouseItem.durability / mouseItem.block.maxToolUses;
                Rect bg = new Rect(mouseRect.x + 4, mouseRect.y + mouseRect.height - 8, mouseRect.width - 8, 4);
                Rect fg = new Rect(mouseRect.x + 4, mouseRect.y + mouseRect.height - 8, (mouseRect.width - 8) * durPct, 4);
                GUI.color = Color.black; GUI.DrawTexture(bg, Texture2D.whiteTexture);
                GUI.color = Color.Lerp(Color.red, Color.green, durPct); GUI.DrawTexture(fg, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            else
            {
                GUIStyle textStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontSize = 14, fontStyle = FontStyle.Bold };
                GUI.Label(mouseRect, $"<color=yellow>{mouseItem.count}</color>", textStyle);
            }
        }
    }

    private void DrawSlot(int index, Rect rect)
    {
        InventorySlot slot = ctrl.inventory[index];
        BlockData renderBlock = slot.block;
        int renderCount = slot.count;
        int renderDur = slot.durability;

        if (isDragging && draggedSlots.Contains(index) && !mouseItem.IsEmpty)
        {
            if (slot.IsEmpty || slot.block == mouseItem.block)
            {
                renderBlock = mouseItem.block;
                renderDur = mouseItem.durability;
                if (dragButton == 0) renderCount += (originalMouseItemCount / draggedSlots.Count);
                else if (dragButton == 1) renderCount += 1;
            }
        }

        GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (index == 27 + ctrl.currentSlotIndex)
        {
            GUI.color = new Color(1, 1, 0, 0.4f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        if (renderBlock != null && renderCount > 0)
        {
            Rect iconRect = new Rect(rect.x + 8, rect.y + 8, rect.width - 16, rect.height - 16);
            GUI.color = renderBlock.blockColor;
            GUI.DrawTexture(iconRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (renderBlock.maxToolUses > 0)
            {
                float durPct = (float)renderDur / renderBlock.maxToolUses;
                Rect bg = new Rect(rect.x + 4, rect.y + rect.height - 8, rect.width - 8, 4);
                Rect fg = new Rect(rect.x + 4, rect.y + rect.height - 8, (rect.width - 8) * durPct, 4);

                GUI.color = Color.black;
                GUI.DrawTexture(bg, Texture2D.whiteTexture);
                GUI.color = Color.Lerp(Color.red, Color.green, durPct);
                GUI.DrawTexture(fg, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            else if (renderCount > 1)
            {
                GUIStyle textStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontSize = 14, fontStyle = FontStyle.Bold };
                GUI.Label(rect, $"<color=yellow>{renderCount}</color>", textStyle);
            }
        }
    }

    private void DoClick(int index, int button)
    {
        if (index == -1) return;
        InventorySlot slot = ctrl.inventory[index];

        if (mouseItem.IsEmpty)
        {
            if (!slot.IsEmpty)
            {
                if (button == 0) { mouseItem = slot.Clone(); slot.Clear(); }
                else if (button == 1) { mouseItem = slot.Clone(); mouseItem.count = Mathf.CeilToInt(slot.count / 2f); slot.count -= mouseItem.count; if (slot.count == 0) slot.Clear(); }
            }
        }
        else
        {
            if (slot.IsEmpty)
            {
                if (button == 0) { slot.block = mouseItem.block; slot.count = mouseItem.count; slot.durability = mouseItem.durability; mouseItem.Clear(); }
                else if (button == 1) { slot.block = mouseItem.block; slot.count = 1; slot.durability = mouseItem.durability; mouseItem.count--; if (mouseItem.count == 0) mouseItem.Clear(); }
            }
            else if (slot.block == mouseItem.block && slot.block.maxToolUses <= 0)
            {
                if (button == 0)
                {
                    int space = slot.MaxStack - slot.count;
                    int transfer = Mathf.Min(space, mouseItem.count);
                    slot.count += transfer; mouseItem.count -= transfer;
                    if (mouseItem.count == 0) mouseItem.Clear();
                }
                else if (button == 1)
                {
                    if (slot.count < slot.MaxStack) { slot.count++; mouseItem.count--; if (mouseItem.count == 0) mouseItem.Clear(); }
                }
            }
            else if (button == 0)
            {
                InventorySlot temp = slot.Clone();
                slot.block = mouseItem.block; slot.count = mouseItem.count; slot.durability = mouseItem.durability;
                mouseItem = temp;
            }
        }
    }

    private void DoDrag()
    {
        if (mouseItem.IsEmpty || mouseItem.block.maxToolUses > 0) return;

        List<InventorySlot> validSlots = new List<InventorySlot>();
        foreach (int i in draggedSlots)
        {
            InventorySlot slot = ctrl.inventory[i];
            if (slot.IsEmpty || slot.block == mouseItem.block) validSlots.Add(slot);
        }

        if (validSlots.Count == 0) return;

        if (dragButton == 0)
        {
            int amountPerSlot = originalMouseItemCount / validSlots.Count;
            if (amountPerSlot > 0)
            {
                foreach (var slot in validSlots)
                {
                    int space = slot.IsEmpty ? 64 : slot.MaxStack - slot.count;
                    int transfer = Mathf.Min(amountPerSlot, space);
                    if (slot.IsEmpty) { slot.block = mouseItem.block; slot.durability = mouseItem.durability; }
                    slot.count += transfer;
                    mouseItem.count -= transfer;
                }
            }
        }
        else if (dragButton == 1)
        {
            foreach (var slot in validSlots)
            {
                if (mouseItem.count <= 0) break;
                int space = slot.IsEmpty ? 64 : slot.MaxStack - slot.count;
                if (space > 0)
                {
                    if (slot.IsEmpty) { slot.block = mouseItem.block; slot.durability = mouseItem.durability; }
                    slot.count += 1;
                    mouseItem.count -= 1;
                }
            }
        }
        if (mouseItem.count <= 0) mouseItem.Clear();
    }

    private void GatherItemsToMouse()
    {
        if (mouseItem.IsEmpty || mouseItem.block.maxToolUses > 0) return;

        int needed = mouseItem.MaxStack - mouseItem.count;
        if (needed <= 0) return;

        for (int i = 0; i < 36; i++)
        {
            InventorySlot slot = ctrl.inventory[i];
            if (slot.IsEmpty || slot.block != mouseItem.block) continue;

            int amountToTake = Mathf.Min(needed, slot.count);
            mouseItem.count += amountToTake;
            slot.count -= amountToTake;
            needed -= amountToTake;

            if (slot.count <= 0) slot.Clear();
            if (needed <= 0) break;
        }
    }
}