using UnityEngine;

public class Mode_Builder : IGameMode
{
    private GameModeController ctrl;
    private int slotIndex = 0;

    // --- ENUMS ---
    public enum BrushType { Paint, Box }
    public enum BrushShape { Cube, Sphere }
    public enum BoxFillMode { Solid, Hollow, Wireframe }

    // --- STATE SETTINGS ---
    private BrushType currentBrush = BrushType.Paint;

    // Paint Settings
    private int brushRadius = 0;       // 0 = 1x1, 1 = 3x3, etc.
    private BrushShape paintShape = BrushShape.Cube;
    private float paintInterval = 0.1f; // Seconds between paints
    private float lastPaintTime = 0f;

    // Box Settings
    private BoxFillMode currentBoxMode = BoxFillMode.Solid;

    // --- MENU STATE ---
    private bool showMenu = false;
    private Rect menuRect;

    // --- INTERACTION STATE ---
    private bool isDragging = false;
    private Vector3Int dragStart;
    private Vector3Int dragEnd;

    public string ModeName => $"Builder ({currentBrush.ToString().ToUpper()})";

    public Mode_Builder(GameModeController c)
    {
        ctrl = c;
        // Make menu larger to fit options
        menuRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 120, 300, 240);
    }

    public void SetupMovement(PlayerMovement move) { }
    public void OnEnter() { ResetState(); }
    public void OnExit() { ResetState(); }

    void ResetState()
    {
        isDragging = false;
        showMenu = false;
    }

    public void OnUpdate(Ray ray)
    {
        // 1. Hotbar Input
        if (Input.GetKeyDown(KeyCode.Alpha1)) slotIndex = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) slotIndex = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) slotIndex = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) slotIndex = 3;
        if (Input.GetKeyDown(KeyCode.Alpha5)) slotIndex = 4;

        // 2. Toggle Menu (F Key)
        if (Input.GetKeyDown(KeyCode.F)) ToggleMenu();

        if (showMenu || Cursor.visible) return;

        // 3. Interaction
        if (Physics.Raycast(ray, out RaycastHit hit, ctrl.interactionRange, ctrl.interactionMask))
        {
            Vector3 centerPoint = hit.point + (hit.normal * 0.1f);
            Vector3Int gridPos = Vector3Int.RoundToInt(centerPoint);
            Vector3Int breakPos = Vector3Int.RoundToInt(hit.point - (hit.normal * 0.1f));

            if (currentBrush == BrushType.Box) HandleBoxLogic(gridPos);
            else HandlePaintLogic(gridPos, breakPos);
        }
        else
        {
            isDragging = false;
        }
    }

    // --- LOGIC HANDLERS ---

    void HandleBoxLogic(Vector3Int gridPos)
    {
        if (Input.GetMouseButtonDown(1)) // Start Drag
        {
            isDragging = true;
            dragStart = gridPos;
        }

        if (isDragging)
        {
            dragEnd = gridPos;

            // Preview Color based on Mode
            Color c = currentBoxMode == BoxFillMode.Solid ? Color.green :
                      currentBoxMode == BoxFillMode.Hollow ? Color.yellow : Color.cyan;

            DrawPreviewBox(dragStart, dragEnd, c);

            if (Input.GetMouseButtonUp(1)) // Execute
            {
                FillBox(dragStart, dragEnd, ctrl.sharedHotbar[slotIndex]);
                isDragging = false;
            }
        }
        else
        {
            DrawPreviewBox(gridPos, gridPos, Color.white);
        }
    }

    void HandlePaintLogic(Vector3Int placePos, Vector3Int breakPos)
    {
        isDragging = false;

        // Draw Paint Cursor Size
        DrawBrushPreview(placePos, brushRadius, paintShape);

        // PLACE (Right Click)
        if (Input.GetMouseButton(1))
        {
            if (Time.time > lastPaintTime + paintInterval)
            {
                BlockData b = ctrl.sharedHotbar[slotIndex];
                if (b != null) ApplyPaint(placePos, b, false); // False = Building
                lastPaintTime = Time.time;
            }
        }

        // BREAK (Left Click)
        if (Input.GetMouseButton(0))
        {
            if (Time.time > lastPaintTime + paintInterval)
            {
                ApplyPaint(breakPos, null, true); // True = Breaking
                lastPaintTime = Time.time;
            }
        }
    }

    // --- ACTION METHODS ---

    void ApplyPaint(Vector3Int center, BlockData block, bool isBreaking)
    {
        // Simple loop for Size
        for (int x = -brushRadius; x <= brushRadius; x++)
        {
            for (int y = -brushRadius; y <= brushRadius; y++)
            {
                for (int z = -brushRadius; z <= brushRadius; z++)
                {
                    Vector3Int pos = center + new Vector3Int(x, y, z);

                    // Shape Check
                    if (paintShape == BrushShape.Sphere)
                    {
                        if (Vector3Int.Distance(center, pos) > brushRadius + 0.5f) continue;
                    }

                    // Optimization: Don't replace identical blocks
                    BlockData current = ctrl.world.GetBlock(pos);
                    if (current != block)
                    {
                        ctrl.world.ModifyBlock(pos, block);
                    }
                }
            }
        }
    }

    void FillBox(Vector3Int start, Vector3Int end, BlockData block)
    {
        Vector3Int min = Vector3Int.Min(start, end);
        Vector3Int max = Vector3Int.Max(start, end);

        int count = (max.x - min.x + 1) * (max.y - min.y + 1) * (max.z - min.z + 1);
        if (count > 8000) { Debug.LogWarning("Selection too large."); return; }

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    bool place = false;

                    // 1. SOLID
                    if (currentBoxMode == BoxFillMode.Solid) place = true;

                    // 2. HOLLOW (Outline) - Place if on any outer face
                    else if (currentBoxMode == BoxFillMode.Hollow)
                    {
                        if (x == min.x || x == max.x ||
                            y == min.y || y == max.y ||
                            z == min.z || z == max.z) place = true;
                    }

                    // 3. WIREFRAME - Place if on two or more edges overlap
                    else if (currentBoxMode == BoxFillMode.Wireframe)
                    {
                        int edgeCount = 0;
                        if (x == min.x || x == max.x) edgeCount++;
                        if (y == min.y || y == max.y) edgeCount++;
                        if (z == min.z || z == max.z) edgeCount++;

                        if (edgeCount >= 2) place = true;
                    }

                    if (place) ctrl.world.ModifyBlock(new Vector3(x, y, z), block);
                }
            }
        }
    }

    // --- UI & GIZMOS ---

    void ToggleMenu()
    {
        showMenu = !showMenu;
        if (showMenu)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            ctrl.movement.InputLocked = true;
            isDragging = false;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            ctrl.movement.InputLocked = false;
        }
    }

    void DrawBrushPreview(Vector3Int center, int r, BrushShape shape)
    {
        // Visualize the paint brush area
        Vector3 min = center - new Vector3(r + 0.5f, r + 0.5f, r + 0.5f);
        Vector3 max = center + new Vector3(r + 0.5f, r + 0.5f, r + 0.5f);

        // Use standard box draw for preview, Sphere implies logic not visual for wireframe usually
        Color c = shape == BrushShape.Sphere ? new Color(1, 0.5f, 0) : Color.white;
        DrawWireBox(min, max, c);
    }

    void DrawPreviewBox(Vector3Int p1, Vector3Int p2, Color c)
    {
        Vector3 min = Vector3.Min(p1, p2) - new Vector3(0.5f, 0.5f, 0.5f);
        Vector3 max = Vector3.Max(p1, p2) + new Vector3(0.5f, 0.5f, 0.5f);
        DrawWireBox(min, max, c);
    }

    void DrawWireBox(Vector3 min, Vector3 max, Color c)
    {
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), c);
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z), c);
        Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), c);
        Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), c);
        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), c);
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), c);
        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), c);
        Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), c);
        Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), c);
        Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), c); // Fixed top rect
        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), c);
        Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), c);
    }

    public void OnGUI()
    {
        if (showMenu)
        {
            GUI.Box(menuRect, "Build Settings");
            float startY = menuRect.y + 30;
            float x = menuRect.x + 20;
            float w = 260;

            // --- MODE SELECTION ---
            GUI.Label(new Rect(x, startY, w, 20), "<b>Tool Mode:</b>");
            if (GUI.Button(new Rect(x, startY + 20, w / 2 - 5, 25), "Paint Brush")) currentBrush = BrushType.Paint;
            if (GUI.Button(new Rect(x + w / 2 + 5, startY + 20, w / 2 - 5, 25), "Box Builder")) currentBrush = BrushType.Box;

            startY += 60;

            // --- DYNAMIC SETTINGS ---
            if (currentBrush == BrushType.Paint)
            {
                GUI.Label(new Rect(x, startY, w, 20), $"Brush Radius: {brushRadius}");
                brushRadius = (int)GUI.HorizontalSlider(new Rect(x, startY + 20, w, 20), brushRadius, 0, 5);

                startY += 45;
                GUI.Label(new Rect(x, startY, w, 20), $"Paint Frequency: {paintInterval:F2}s");
                paintInterval = GUI.HorizontalSlider(new Rect(x, startY + 20, w, 20), paintInterval, 0.05f, 0.5f);

                startY += 45;
                GUI.Label(new Rect(x, startY, w, 20), "Brush Shape:");
                if (GUI.Button(new Rect(x, startY + 20, w / 2 - 5, 25), "Cube")) paintShape = BrushShape.Cube;
                if (GUI.Button(new Rect(x + w / 2 + 5, startY + 20, w / 2 - 5, 25), "Sphere")) paintShape = BrushShape.Sphere;
            }
            else
            {
                GUI.Label(new Rect(x, startY, w, 20), "Box Fill Mode:");
                if (GUI.Button(new Rect(x, startY + 20, w, 25), $"Current: {currentBoxMode}"))
                {
                    // Cycle modes
                    int next = (int)currentBoxMode + 1;
                    if (next > 2) next = 0;
                    currentBoxMode = (BoxFillMode)next;
                }
                GUI.Label(new Rect(x, startY + 50, w, 40), "<size=10>(Solid: Fill All)\n(Hollow: Walls Only)\n(Wireframe: Edges Only)</size>");
            }
        }

        string toolInfo = currentBrush == BrushType.Paint
            ? $"PAINT | R: {brushRadius} | {paintShape}"
            : $"BOX | {currentBoxMode}";

        GUI.Label(new Rect(20, Screen.height - 50, 500, 30), $"[{toolInfo}] [F: Settings] | Slot {slotIndex + 1}");
    }
}