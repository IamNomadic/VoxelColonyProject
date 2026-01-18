using UnityEngine;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Mode_Scanner : IGameMode
{
    private GameModeController ctrl;

    // Toggle
    private bool isBlockMode = false;

    // Selection
    private Vector3Int? pointA;
    private Vector3Int? pointB;
    private Vector3Int currentCursor;

    // UI State
    private bool isTypingName = false;
    private string saveName = "MyBlock";
    private Rect saveWindowRect;

    public string ModeName => isBlockMode ? "Scanner (MULTI-RES)" : "Scanner (STRUCTURE)";

    public Mode_Scanner(GameModeController c)
    {
        ctrl = c;
        saveWindowRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 50, 300, 120);
    }

    public void SetupMovement(PlayerMovement move) { }
    public void OnEnter() { }
    public void OnExit() { ClearSelection(); isTypingName = false; }

    public void OnUpdate(Ray ray)
    {
        // 0. UI Blocking
        if (isTypingName)
        {
            HandleTypingInput();
            return;
        }

        // 1. Toggle Mode
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isBlockMode = !isBlockMode;
            ClearSelection();
            Debug.Log($"Switched Scanner to {(isBlockMode ? "MICRO-BLOCK" : "STRUCTURE")}");
        }

        // 2. Raycast
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, ctrl.interactionRange, ctrl.interactionMask);
        if (hitSomething)
        {
            Vector3 p = hit.point - (hit.normal * 0.1f);
            currentCursor = new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
        }

        // 3. Input
        if (hitSomething)
        {
            if (Input.GetMouseButtonDown(0)) pointA = currentCursor;
            if (Input.GetMouseButtonDown(1)) pointB = currentCursor;
        }

        if (Input.GetKeyDown(KeyCode.C)) ClearSelection();

        // 4. Logic
        if (isBlockMode) HandleBlockMode();
        else HandleStructureMode();
    }

    void HandleBlockMode()
    {
        if (pointA.HasValue && pointB.HasValue)
        {
            BoundsInt bounds = GetSelectionBounds(pointA.Value, pointB.Value);
            int size = bounds.size.x;

            // VALIDATION: Must be Cubic AND one of our supported sizes
            bool isCubic = (bounds.size.x == bounds.size.y && bounds.size.x == bounds.size.z);
            bool isSupportedSize = (size == 8 || size == 16 || size == 32);
            bool isValid = isCubic && isSupportedSize;

            DrawSelectionBox(pointA.Value, pointB.Value, isValid ? Color.green : Color.red);

            if (Input.GetKeyDown(KeyCode.G))
            {
                if (isValid)
                {
                    saveName = $"Block_{size}x";
                    isTypingName = true;
                }
                else
                {
                    Debug.LogError($"Invalid Selection: {bounds.size}. Must be cubic 8x, 16x, or 32x.");
                }
            }
        }
        else if (pointA.HasValue)
        {
            DrawSelectionBox(pointA.Value, currentCursor, Color.yellow);
        }
    }

    void PerformSave()
    {
        if (isBlockMode)
        {
            BoundsInt bounds = GetSelectionBounds(pointA.Value, pointB.Value);
            CaptureMicroModel(bounds.min, bounds.size.x, saveName);
        }
        else
        {
            Debug.Log($"Saving Structure '{saveName}' (Placeholder)");
        }

        isTypingName = false;
        ctrl.movement.InputLocked = false;
        ClearSelection();
    }

    void CaptureMicroModel(Vector3Int start, int res, string filename)
    {
        MicroModelData data = new MicroModelData();
        data.resolution = res; // IMPORTANT: Save the detected resolution
        data.voxels = new Color32[res * res * res];

        int foundVoxels = 0;

        for (int x = 0; x < res; x++)
        {
            for (int y = 0; y < res; y++)
            {
                for (int z = 0; z < res; z++)
                {
                    BlockData b = ctrl.world.GetBlock(new Vector3(start.x + x, start.y + y, start.z + z));
                    // Dynamic Index
                    int index = x + (y * res) + (z * res * res);

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

        // Save Logic
        string json = JsonUtility.ToJson(data, true);
        string folderPath = Path.Combine(Application.dataPath, "SavedBlocks");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
        string fullPath = Path.Combine(folderPath, filename + ".json");
        File.WriteAllText(fullPath, json);

        Debug.Log($"<color=green>Saved {res}x Model to Assets/SavedBlocks/{filename}.json</color>");

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
    }

    // --- BOILERPLATE HELPERS ---
    void HandleTypingInput()
    {
        ctrl.movement.InputLocked = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) PerformSave();
        if (Input.GetKeyDown(KeyCode.Escape)) { isTypingName = false; ctrl.movement.InputLocked = false; }
    }

    void HandleStructureMode()
    {
        if (pointA.HasValue && pointB.HasValue)
        {
            DrawSelectionBox(pointA.Value, pointB.Value, Color.cyan);
            if (Input.GetKeyDown(KeyCode.G)) { saveName = "Structure"; isTypingName = true; }
        }
        else if (pointA.HasValue) DrawSelectionBox(pointA.Value, currentCursor, Color.cyan);
    }

    void ClearSelection() { pointA = null; pointB = null; }
    BoundsInt GetSelectionBounds(Vector3Int p1, Vector3Int p2)
    {
        Vector3Int min = Vector3Int.Min(p1, p2);
        Vector3Int max = Vector3Int.Max(p1, p2);
        return new BoundsInt(min, (max - min) + Vector3Int.one);
    }
    void DrawSelectionBox(Vector3Int p1, Vector3Int p2, Color c)
    {
        BoundsInt b = GetSelectionBounds(p1, p2);
        Vector3 min = b.min; Vector3 max = b.max;
        // (DrawLines omitted for brevity - same as previous script)
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
        if (isTypingName)
        {
            GUI.Box(saveWindowRect, "Save As...");
            saveName = GUI.TextField(new Rect(saveWindowRect.x + 20, saveWindowRect.y + 30, 260, 30), saveName, 25);
            if (GUI.Button(new Rect(saveWindowRect.x + 20, saveWindowRect.y + 70, 120, 30), "Save")) PerformSave();
            if (GUI.Button(new Rect(saveWindowRect.x + 160, saveWindowRect.y + 70, 120, 30), "Cancel")) { isTypingName = false; ctrl.movement.InputLocked = false; }
            return;
        }
        string mode = isBlockMode ? "MICRO-BLOCK (8x/16x/32x)" : "STRUCTURE MODE";
        string info = "Select Area | G: Save";
        if (isBlockMode && pointA.HasValue)
        {
            Vector3Int p2 = pointB.HasValue ? pointB.Value : currentCursor;
            BoundsInt b = GetSelectionBounds(pointA.Value, p2);
            bool ok = (b.size.x == b.size.y && b.size.x == b.size.z) && (b.size.x == 8 || b.size.x == 16 || b.size.x == 32);
            string c = ok ? "green" : "red";
            info = $"Size: <color={c}>{b.size.x}</color> (Must be 8, 16, or 32)";
        }
        GUI.Label(new Rect(20, Screen.height - 70, 400, 30), $"<b>{mode}</b>");
        GUI.Label(new Rect(20, Screen.height - 50, 400, 30), info);
    }
}