using UnityEngine;
using System.Collections.Generic;

public class Mode_Builder : IGameMode
{
    private GameModeController ctrl;
    private int slotIndex = 0;

    // --- ENUMS ---
    public enum BrushType { Paint, Box, Line, Replace }
    public enum BrushShape { Cube, Sphere, Pyramid }
    public enum BrushFill { Solid, Hollow, Wireframe }

    // --- PALETTE DATA ---
    [System.Serializable]
    public class PaletteItem
    {
        public BlockData block;
        public float weight = 1.0f;
    }
    private List<PaletteItem> replacePalette = new List<PaletteItem>();

    // --- GLOBAL SETTINGS ---
    private BrushType currentTool = BrushType.Paint;
    private BrushShape currentShape = BrushShape.Cube;
    private BrushFill currentFill = BrushFill.Solid;

    // --- BRUSH STATE ---
    private int brushRadius = 0;
    private float paintInterval = 0.1f;
    private float lastPaintTime = 0f;

    // --- DRAG STATE ---
    private bool isDragging = false;
    private Vector3Int dragStart;
    private Vector3Int dragEnd;

    // --- UI STATE ---
    private bool showMenu = false;
    private bool showBlockPicker = false; // New Picker State
    private Rect menuRect;
    private Vector2 paletteScroll;
    private Vector2 pickerScroll;

    // Cache for the picker list so we don't rebuild it every frame
    private List<BlockData> allAvailableBlocks = new List<BlockData>();

    public string ModeName => $"Builder ({currentTool.ToString().ToUpper()})";

    public Mode_Builder(GameModeController c)
    {
        ctrl = c;
        menuRect = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 200, 400, 400);
    }

    public void SetupMovement(PlayerMovement move) { move.SetFlying(true); } // TURN ON FLIGHT

    public void OnEnter() { ResetState(); }
    public void OnExit() { ResetState(); }

    void ResetState()
    {
        isDragging = false;
        showMenu = false;
        showBlockPicker = false;
    }

    // --- CACHE BLOCK LIST ---
    void RefreshBlockList()
    {
        allAvailableBlocks.Clear();
        if (BlockManager.Instance != null)
        {
            // Add standard blocks
            if (BlockManager.Instance.loadedBlocks != null)
                allAvailableBlocks.AddRange(BlockManager.Instance.loadedBlocks);

            // Add runtime generated colors
            if (BlockManager.Instance.colorPalette != null)
                allAvailableBlocks.AddRange(BlockManager.Instance.colorPalette);
        }
    }

    public void OnUpdate(Ray ray)
    {
        // 1. Hotbar Input
        if (Input.GetKeyDown(KeyCode.Alpha1)) slotIndex = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) slotIndex = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) slotIndex = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) slotIndex = 3;
        if (Input.GetKeyDown(KeyCode.Alpha5)) slotIndex = 4;

        // 2. Menu Toggle
        if (Input.GetKeyDown(KeyCode.F)) ToggleMenu();

        if (showMenu || Cursor.visible) return;

        // 3. Interaction
        if (Physics.Raycast(ray, out RaycastHit hit, ctrl.interactionRange, ctrl.interactionMask))
        {
            Vector3 pointIn = hit.point - (hit.normal * 0.05f);
            Vector3 pointOut = hit.point + (hit.normal * 0.05f);

            Vector3Int breakPos = new Vector3Int(Mathf.FloorToInt(pointIn.x), Mathf.FloorToInt(pointIn.y), Mathf.FloorToInt(pointIn.z));
            Vector3Int placePos = new Vector3Int(Mathf.FloorToInt(pointOut.x), Mathf.FloorToInt(pointOut.y), Mathf.FloorToInt(pointOut.z));

            if (currentTool == BrushType.Box) HandleBoxLogic(placePos);
            else if (currentTool == BrushType.Line) HandleLineLogic(placePos);
            else if (currentTool == BrushType.Replace) HandleReplaceLogic(breakPos);
            else HandlePaintLogic(placePos, breakPos);
        }
        else
        {
            isDragging = false;
        }
    }

    // --- TOOL LOGIC HANDLERS ---
    void HandleBoxLogic(Vector3Int gridPos)
    {
        if (Input.GetMouseButtonDown(1)) { isDragging = true; dragStart = gridPos; }
        if (isDragging)
        {
            dragEnd = gridPos;
            DrawPreviewBounds(dragStart, dragEnd, GetPreviewColor());
            if (Input.GetMouseButtonUp(1)) { FillArea(dragStart, dragEnd, ctrl.sharedHotbar[slotIndex]); isDragging = false; }
        }
        else DrawPreviewBounds(gridPos, gridPos, Color.white);
    }

    void HandleReplaceLogic(Vector3Int gridPos)
    {
        if (Input.GetMouseButtonDown(1)) { isDragging = true; dragStart = gridPos; }
        if (isDragging)
        {
            dragEnd = gridPos;
            DrawPreviewBounds(dragStart, dragEnd, Color.magenta);
            if (Input.GetMouseButtonUp(1)) { ReplaceArea(dragStart, dragEnd); isDragging = false; }
        }
        else DrawPreviewBounds(gridPos, gridPos, Color.magenta);
    }

    void HandleLineLogic(Vector3Int gridPos)
    {
        if (Input.GetMouseButtonDown(1)) { isDragging = true; dragStart = gridPos; }
        if (isDragging)
        {
            dragEnd = gridPos;
            Debug.DrawLine(dragStart, dragEnd, Color.cyan);
            DrawBrushPreview(dragStart, Color.white);
            DrawBrushPreview(dragEnd, Color.white);
            if (Input.GetMouseButtonUp(1)) { Draw3DLine(dragStart, dragEnd, ctrl.sharedHotbar[slotIndex]); isDragging = false; }
        }
        else DrawBrushPreview(gridPos, Color.white);
    }

    void HandlePaintLogic(Vector3Int placePos, Vector3Int breakPos)
    {
        isDragging = false;
        DrawBrushPreview(placePos, Color.cyan);
        if (Input.GetMouseButton(1))
        {
            if (Time.time > lastPaintTime + paintInterval) { StampBrush(placePos, ctrl.sharedHotbar[slotIndex]); lastPaintTime = Time.time; }
        }
        if (Input.GetMouseButton(0))
        {
            if (Time.time > lastPaintTime + paintInterval) { StampBrush(breakPos, null); lastPaintTime = Time.time; }
        }
    }

    // --- REPLACEMENT ALGORITHM ---
    void ReplaceArea(Vector3Int start, Vector3Int end)
    {
        if (replacePalette.Count == 0) { Debug.LogWarning("Replace Palette is empty!"); return; }

        Vector3Int min = Vector3Int.Min(start, end);
        Vector3Int max = Vector3Int.Max(start, end);
        float totalWeight = 0f;
        foreach (var item in replacePalette) totalWeight += item.weight;

        for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
                for (int z = min.z; z <= max.z; z++)
                {
                    BlockData existing = ctrl.world.GetBlock(new Vector3(x, y, z));
                    if (existing != null)
                    {
                        BlockData selected = PickRandomFromPalette(totalWeight);
                        if (selected != null && selected != existing)
                            ctrl.world.ModifyBlock(new Vector3(x, y, z), selected);
                    }
                }
    }

    BlockData PickRandomFromPalette(float totalWeight)
    {
        float roll = Random.Range(0f, totalWeight);
        float current = 0f;
        foreach (var item in replacePalette)
        {
            current += item.weight;
            if (roll <= current) return item.block;
        }
        return replacePalette[0].block;
    }

    // --- FILL ALGORITHMS ---
    void StampBrush(Vector3Int center, BlockData block)
    {
        Vector3Int min = center - new Vector3Int(brushRadius, brushRadius, brushRadius);
        Vector3Int max = center + new Vector3Int(brushRadius, brushRadius, brushRadius);
        FillArea(min, max, block);
    }

    void Draw3DLine(Vector3Int start, Vector3Int end, BlockData block)
    {
        float dist = Vector3.Distance(start, end);
        int steps = Mathf.CeilToInt(dist * 2f);
        if (steps < 1) steps = 1;
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 pos = Vector3.Lerp(start, end, t);
            Vector3Int gridPos = new Vector3Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
            StampBrush(gridPos, block);
        }
    }

    void FillArea(Vector3Int min, Vector3Int max, BlockData block)
    {
        Vector3Int realMin = Vector3Int.Min(min, max);
        Vector3Int realMax = Vector3Int.Max(min, max);
        long volume = (long)(realMax.x - realMin.x + 1) * (realMax.y - realMin.y + 1) * (realMax.z - realMin.z + 1);
        if (volume > 16000) { Debug.LogWarning("Selection too large."); return; }

        Vector3 center = (Vector3)(realMin + realMax) / 2f;
        Vector3 radius = (Vector3)(realMax - realMin) / 2f;
        radius = Vector3.Max(radius, new Vector3(0.5f, 0.5f, 0.5f));

        for (int x = realMin.x; x <= realMax.x; x++)
            for (int y = realMin.y; y <= realMax.y; y++)
                for (int z = realMin.z; z <= realMax.z; z++)
                {
                    if (ShouldPlace(x, y, z, realMin, realMax, center, radius))
                        ctrl.world.ModifyBlock(new Vector3(x, y, z), block);
                }
    }

    bool ShouldPlace(int x, int y, int z, Vector3Int min, Vector3Int max, Vector3 center, Vector3 radius)
    {
        if (currentShape == BrushShape.Cube)
        {
            if (currentFill == BrushFill.Solid) return true;
            bool isWall = (x == min.x || x == max.x || y == min.y || y == max.y || z == min.z || z == max.z);
            if (currentFill == BrushFill.Hollow) return isWall;
            if (currentFill == BrushFill.Wireframe)
            {
                int edges = 0;
                if (x == min.x || x == max.x) edges++;
                if (y == min.y || y == max.y) edges++;
                if (z == min.z || z == max.z) edges++;
                return edges >= 2;
            }
        }
        else if (currentShape == BrushShape.Sphere)
        {
            float dx = (x - center.x) / radius.x;
            float dy = (y - center.y) / radius.y;
            float dz = (z - center.z) / radius.z;
            float distSq = (dx * dx) + (dy * dy) + (dz * dz);

            if (distSq > 1.1f) return false;
            if (currentFill == BrushFill.Solid) return true;
            if (currentFill == BrushFill.Hollow)
            {
                float normalizedThickness = 1.0f - (0.9f / Mathf.Max(radius.x, 1f));
                return distSq > (normalizedThickness * normalizedThickness);
            }
            if (currentFill == BrushFill.Wireframe)
            {
                bool onXPlane = Mathf.Abs(x - center.x) <= 0.5f;
                bool onYPlane = Mathf.Abs(y - center.y) <= 0.5f;
                bool onZPlane = Mathf.Abs(z - center.z) <= 0.5f;
                return (distSq > 0.8f) && (onXPlane || onYPlane || onZPlane);
            }
        }
        else if (currentShape == BrushShape.Pyramid)
        {
            float height = max.y - min.y;
            if (height < 0.1f) return true;
            float yLevel = y - min.y;
            float progress = yLevel / height;
            float limitX = radius.x * (1f - progress);
            float limitZ = radius.z * (1f - progress);
            float dx = Mathf.Abs(x - center.x);
            float dz = Mathf.Abs(z - center.z);

            if (!((dx <= limitX + 0.5f) && (dz <= limitZ + 0.5f))) return false;

            if (currentFill == BrushFill.Solid) return true;
            if (currentFill == BrushFill.Hollow)
            {
                if (y == min.y) return true;
                return (dx >= limitX - 0.5f || dz >= limitZ - 0.5f);
            }
            if (currentFill == BrushFill.Wireframe)
            {
                if (y == min.y) return (dx >= radius.x - 0.5f || dz >= radius.z - 0.5f);
                return (dx >= limitX - 0.5f) && (dz >= limitZ - 0.5f);
            }
        }
        return false;
    }

    // --- UI HELPERS ---
    Color GetPreviewColor()
    {
        if (currentTool == BrushType.Replace) return Color.magenta;
        if (currentFill == BrushFill.Solid) return Color.green;
        if (currentFill == BrushFill.Hollow) return Color.yellow;
        return new Color(1, 0, 1);
    }

    void ToggleMenu()
    {
        showMenu = !showMenu;
        showBlockPicker = false;
        Cursor.visible = showMenu;
        Cursor.lockState = showMenu ? CursorLockMode.None : CursorLockMode.Locked;
        ctrl.movement.InputLocked = showMenu;
        isDragging = false;

        // Refresh block list only when opening menu
        if (showMenu) RefreshBlockList();
    }

    void DrawBrushPreview(Vector3Int center, Color c)
    {
        Vector3Int min = center - new Vector3Int(brushRadius, brushRadius, brushRadius);
        Vector3Int max = center + new Vector3Int(brushRadius, brushRadius, brushRadius);
        DrawPreviewBounds(min, max, c);
    }

    void DrawPreviewBounds(Vector3Int p1, Vector3Int p2, Color c)
    {
        Vector3 min = Vector3.Min(p1, p2) - new Vector3(0.5f, 0.5f, 0.5f);
        Vector3 max = Vector3.Max(p1, p2) + new Vector3(0.5f, 0.5f, 0.5f);
        Vector3 vMin = new Vector3(min.x, min.y, min.z);
        Vector3 vMax = new Vector3(max.x + 1f, max.y + 1f, max.z + 1f);

        Debug.DrawLine(new Vector3(vMin.x, vMin.y, vMin.z), new Vector3(vMax.x, vMin.y, vMin.z), c);
        Debug.DrawLine(new Vector3(vMin.x, vMin.y, vMin.z), new Vector3(vMin.x, vMin.y, vMax.z), c);
        Debug.DrawLine(new Vector3(vMax.x, vMin.y, vMax.z), new Vector3(vMax.x, vMin.y, vMin.z), c);
        Debug.DrawLine(new Vector3(vMax.x, vMin.y, vMax.z), new Vector3(vMin.x, vMin.y, vMax.z), c);
        Debug.DrawLine(new Vector3(vMin.x, vMax.y, vMin.z), new Vector3(vMax.x, vMax.y, vMin.z), c);
        Debug.DrawLine(new Vector3(vMin.x, vMax.y, vMin.z), new Vector3(vMin.x, vMax.y, vMax.z), c);
        Debug.DrawLine(new Vector3(vMax.x, vMax.y, vMax.z), new Vector3(vMax.x, vMax.y, vMin.z), c);
        Debug.DrawLine(new Vector3(vMax.x, vMax.y, vMax.z), new Vector3(vMin.x, vMax.y, vMax.z), c);
        Debug.DrawLine(new Vector3(vMin.x, vMin.y, vMin.z), new Vector3(vMin.x, vMax.y, vMin.z), c);
        Debug.DrawLine(new Vector3(vMax.x, vMin.y, vMin.z), new Vector3(vMax.x, vMax.y, vMin.z), c);
        Debug.DrawLine(new Vector3(vMin.x, vMin.y, vMax.z), new Vector3(vMin.x, vMax.y, vMax.z), c);
        Debug.DrawLine(new Vector3(vMax.x, vMin.y, vMax.z), new Vector3(vMax.x, vMax.y, vMax.z), c);
    }

    public void OnGUI()
    {
        if (showMenu)
        {
            if (showBlockPicker)
            {
                // --- BLOCK PICKER SUB-MENU ---
                GUI.Box(menuRect, "<b>Select a Block</b>");
                float px = menuRect.x + 10;
                float py = menuRect.y + 30;
                float pw = menuRect.width - 20;

                if (GUI.Button(new Rect(px, py, 60, 25), "Back")) showBlockPicker = false;

                pickerScroll = GUI.BeginScrollView(
                    new Rect(px, py + 30, pw, menuRect.height - 70),
                    pickerScroll,
                    new Rect(0, 0, pw - 20, allAvailableBlocks.Count * 25)
                );

                for (int i = 0; i < allAvailableBlocks.Count; i++)
                {
                    BlockData b = allAvailableBlocks[i];
                    if (GUI.Button(new Rect(0, i * 25, pw - 20, 25), b.blockName))
                    {
                        replacePalette.Add(new PaletteItem { block = b, weight = 1f });
                        showBlockPicker = false;
                    }
                }
                GUI.EndScrollView();
            }
            else
            {
                // --- MAIN SETTINGS MENU ---
                GUI.Box(menuRect, "<b>Builder Settings</b>");
                float x = menuRect.x + 20;
                float y = menuRect.y + 30;
                float w = 360;

                // 1. TOOL SELECT
                GUI.Label(new Rect(x, y, w, 20), "Tool Type:");
                float w4 = w / 4 - 15;
                if (GUI.Button(new Rect(x, y + 20, w4, 25), "Paint")) currentTool = BrushType.Paint;
                if (GUI.Button(new Rect(x + w4 + 2, y + 20, w4, 25), "Box")) currentTool = BrushType.Box;
                if (GUI.Button(new Rect(x + (w4 + 2) * 2, y + 20, w4, 25), "Line")) currentTool = BrushType.Line;
                if (GUI.Button(new Rect(x + (w4 + 2) * 3, y + 20, w4, 25), "Repl.")) currentTool = BrushType.Replace;
                y += 55;

                if (currentTool == BrushType.Replace)
                {
                    // --- REPLACE PALETTE UI ---
                    if (GUI.Button(new Rect(x, y, w - 40, 30), "Add New Block..."))
                    {
                        showBlockPicker = true;
                    }
                    y += 35;

                    GUI.Label(new Rect(x, y, w, 20), "Palette (Weight):");
                    y += 20;

                    paletteScroll = GUI.BeginScrollView(
                        new Rect(x, y, w - 40, 240),
                        paletteScroll,
                        new Rect(0, 0, w - 60, replacePalette.Count * 35)
                    );

                    for (int i = 0; i < replacePalette.Count; i++)
                    {
                        PaletteItem item = replacePalette[i];
                        GUI.Label(new Rect(0, i * 35, 120, 25), item.block.blockName);
                        item.weight = GUI.HorizontalSlider(new Rect(125, i * 35 + 5, 100, 20), item.weight, 0.1f, 10f);
                        GUI.Label(new Rect(230, i * 35, 40, 25), item.weight.ToString("F1"));
                        if (GUI.Button(new Rect(270, i * 35, 30, 25), "X"))
                        {
                            replacePalette.RemoveAt(i);
                            i--;
                        }
                    }
                    GUI.EndScrollView();
                }
                else
                {
                    // --- STANDARD TOOLS UI ---
                    GUI.Label(new Rect(x, y, w, 20), "Shape:");
                    float wShape = (w - 40) / 3;
                    if (GUI.Button(new Rect(x, y + 20, wShape, 25), "Cube")) currentShape = BrushShape.Cube;
                    if (GUI.Button(new Rect(x + wShape + 2, y + 20, wShape, 25), "Sphere")) currentShape = BrushShape.Sphere;
                    if (GUI.Button(new Rect(x + (wShape + 2) * 2, y + 20, wShape, 25), "Pyramid")) currentShape = BrushShape.Pyramid;
                    y += 55;

                    GUI.Label(new Rect(x, y, w, 20), "Fill Mode:");
                    float wFill = (w - 40) / 3;
                    if (GUI.Button(new Rect(x, y + 20, wFill, 25), "Solid")) currentFill = BrushFill.Solid;
                    if (GUI.Button(new Rect(x + wFill + 2, y + 20, wFill, 25), "Hollow")) currentFill = BrushFill.Hollow;
                    if (GUI.Button(new Rect(x + (wFill + 2) * 2, y + 20, wFill, 25), "Wire")) currentFill = BrushFill.Wireframe;
                    y += 55;

                    if (currentTool != BrushType.Box)
                    {
                        GUI.Label(new Rect(x, y, w, 20), $"Size (Radius): {brushRadius}");
                        brushRadius = (int)GUI.HorizontalSlider(new Rect(x, y + 20, w - 40, 20), brushRadius, 0, 8);
                    }
                }
            }
        }

        string info = $"[{currentTool.ToString().ToUpper()}]";
        if (currentTool == BrushType.Replace) info += $" [Palette: {replacePalette.Count}]";
        else info += $" [{currentShape}] [{currentFill}] [R:{brushRadius}]";

        GUI.Label(new Rect(20, Screen.height - 50, 600, 30), $"{info} [F: Menu] | Slot {slotIndex + 1}");
    }
}