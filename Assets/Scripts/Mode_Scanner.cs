using UnityEngine;
using System.IO;

public class Mode_Scanner : IGameMode
{
    private GameModeController ctrl;

    // --- MODE TOGGLE ---
    private bool isBlockMode = false;

    // Unified Selection Data
    private Vector3Int? pointA;
    private Vector3Int? pointB;

    // Cursor Tracking
    private Vector3Int currentCursor;

    public string ModeName => isBlockMode ? "Scanner (MICRO-BLOCK)" : "Scanner (STRUCTURE)";

    public Mode_Scanner(GameModeController c) { ctrl = c; }
    public void SetupMovement(PlayerMovement move) { }
    public void OnEnter() { }
    public void OnExit() { ClearSelection(); }

    public void OnUpdate(Ray ray)
    {
        // 1. Toggle Sub-Mode
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isBlockMode = !isBlockMode;
            ClearSelection(); // Clear points when switching modes to avoid confusion
            Debug.Log($"<color=yellow>Switched Scanner to {(isBlockMode ? "MICRO-BLOCK (8x8x8)" : "STRUCTURE")}</color>");
        }

        // 2. Raycast (Track Cursor)
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, ctrl.interactionRange, ctrl.interactionMask);
        if (hitSomething)
        {
            Vector3 p = hit.point - (hit.normal * 0.1f);
            currentCursor = new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
        }

        // 3. Input (Universal L-Click / R-Click)
        if (hitSomething)
        {
            if (Input.GetMouseButtonDown(0)) pointA = currentCursor; // Start Point
            if (Input.GetMouseButtonDown(1)) pointB = currentCursor; // End Point
        }

        // 4. Clear Selection
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearSelection();
            Debug.Log("Selection Cleared.");
        }

        // 5. Logic Dispatch
        if (isBlockMode) HandleBlockMode();
        else HandleStructureMode();
    }

    void HandleStructureMode()
    {
        // Visuals: Draw Box if we have points, or Line if we only have A
        if (pointA.HasValue && pointB.HasValue)
        {
            DrawSelectionBox(pointA.Value, pointB.Value, Color.cyan);

            // Save Trigger
            if (Input.GetKeyDown(KeyCode.G))
            {
                Debug.Log("Saving Structure (Placeholder)...");
                // Structure save logic goes here...
            }
        }
        else if (pointA.HasValue)
        {
            // Draw Preview from A to Cursor
            DrawSelectionBox(pointA.Value, currentCursor, Color.cyan);
        }
    }

    void HandleBlockMode()
    {
        if (pointA.HasValue && pointB.HasValue)
        {
            // 1. Calculate Dimensions
            BoundsInt bounds = GetSelectionBounds(pointA.Value, pointB.Value);

            // 2. Validate Size (Must be 8x8x8)
            bool isValid = (bounds.size.x == 8 && bounds.size.y == 8 && bounds.size.z == 8);

            // 3. Draw Box (Green = Good, Red = Bad)
            DrawSelectionBox(pointA.Value, pointB.Value, isValid ? Color.green : Color.red);

            // 4. Save Trigger
            if (Input.GetKeyDown(KeyCode.G))
            {
                if (isValid)
                {
                    CaptureMicroModel(bounds.min); // Pass the bottom-left corner
                }
                else
                {
                    Debug.LogError($"Invalid Size: {bounds.size}. Must be exactly 8x8x8!");
                }
            }
        }
        else if (pointA.HasValue)
        {
            // Preview current drag
            BoundsInt bounds = GetSelectionBounds(pointA.Value, currentCursor);
            bool isValid = (bounds.size.x == 8 && bounds.size.y == 8 && bounds.size.z == 8);
            DrawSelectionBox(pointA.Value, currentCursor, isValid ? Color.green : Color.red);
        }
    }

    void CaptureMicroModel(Vector3Int start)
    {
        MicroModelData data = new MicroModelData();
        int foundVoxels = 0;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                for (int z = 0; z < 8; z++)
                {
                    // Scan relative to the start point (Min)
                    BlockData b = ctrl.world.GetBlock(new Vector3(start.x + x, start.y + y, start.z + z));
                    int index = x + (y * 8) + (z * 64);

                    if (b != null)
                    {
                        data.voxels[index] = b.blockColor;
                        foundVoxels++;
                    }
                    else
                    {
                        data.voxels[index] = new Color32(0, 0, 0, 0);
                    }
                }
            }
        }

        string json = JsonUtility.ToJson(data, true);
        string filename = $"MicroModel_{System.DateTime.Now.Ticks}.json";
        string path = Path.Combine(Application.persistentDataPath, filename);

        File.WriteAllText(path, json);
        Debug.Log($"<color=green>Saved Micro-Model ({foundVoxels} voxels) to:</color> {path}");
        // Clear selection to indicate success
        ClearSelection();
    }

    // --- HELPERS ---

    void ClearSelection() { pointA = null; pointB = null; }

    BoundsInt GetSelectionBounds(Vector3Int p1, Vector3Int p2)
    {
        Vector3Int min = Vector3Int.Min(p1, p2);
        Vector3Int max = Vector3Int.Max(p1, p2);
        // Add 1 because selection is inclusive (e.g. 0 to 0 is size 1)
        return new BoundsInt(min, (max - min) + Vector3Int.one);
    }

    void DrawSelectionBox(Vector3Int p1, Vector3Int p2, Color c)
    {
        BoundsInt b = GetSelectionBounds(p1, p2);
        Vector3 min = b.min;
        Vector3 max = b.max; // This is actually min + size

        // Draw Box Edges
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), c);
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z), c);
        Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), c);
        Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), c);

        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), c);
        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), c);
        Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), c);
        Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), c);

        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), c);
        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), c);
        Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), c);
        Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), c);
    }

    public void OnGUI()
    {
        string mode = isBlockMode ? "MICRO-BLOCK MODE" : "STRUCTURE MODE";
        string info = "L-Click: Point A | R-Click: Point B | G: Save | C: Clear";

        if (isBlockMode && pointA.HasValue)
        {
            Vector3Int p2 = pointB.HasValue ? pointB.Value : currentCursor;
            BoundsInt b = GetSelectionBounds(pointA.Value, p2);
            string color = (b.size.x == 8 && b.size.y == 8 && b.size.z == 8) ? "green" : "red";
            info = $"Size: <color={color}>{b.size}</color> (Must be 8x8x8)";
        }

        GUI.Label(new Rect(20, Screen.height - 70, 400, 30), $"<b>{mode}</b> (Tab to Switch)");
        GUI.Label(new Rect(20, Screen.height - 50, 400, 30), info);
    }
}