
using UnityEngine;
using System.IO;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Mode_Scanner : IGameMode
{
    private GameModeController ctrl;

    // Toggles
    private bool isBlockMode = false;

    // Selection State
    private Vector3Int? pointA;
    private Vector3Int? pointB;
    private Vector3Int currentCursor;

    // UI State
    private bool isTypingName = false;
    private string saveName = "MyStructure";
    private Rect saveWindowRect;

    public string ModeName => isBlockMode ? "Scanner (MICRO-BLOCK)" : "Scanner (STRUCTURE)";

    public Mode_Scanner(GameModeController c)
    {
        ctrl = c;
        saveWindowRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 50, 300, 120);
    }

    public void SetupMovement(PlayerMovement move) { move.SetFlying(true); } // TURN ON FLIGHT

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

        // 1. Toggle Mode (Tab)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isBlockMode = !isBlockMode;
            ClearSelection();
            Debug.Log($"Switched Scanner to {(isBlockMode ? "MICRO-BLOCK" : "STRUCTURE")}");
        }

        // 2. Raycast & Cursor Update
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, ctrl.interactionRange, ctrl.interactionMask);
        if (hitSomething)
        {
            // Point slightly out to select the empty space (if building) or slightly in (if scanning blocks)
            // We use RoundToInt to snap to grid center
            Vector3 p = hit.point - (hit.normal * 0.1f);
            currentCursor = Vector3Int.RoundToInt(p);
        }

        // 3. Input Handling
        if (hitSomething)
        {
            if (Input.GetMouseButtonDown(0)) pointA = currentCursor;
            if (Input.GetMouseButtonDown(1)) pointB = currentCursor;
        }

        if (Input.GetKeyDown(KeyCode.C)) ClearSelection();

        // 4. Visualization & Save Trigger
        DrawSelectionLoop();
    }

    // --- MAIN LOGIC LOOP ---
    void DrawSelectionLoop()
    {
        if (pointA.HasValue)
        {
            Vector3Int p2 = pointB.HasValue ? pointB.Value : currentCursor;

            // Color Logic: Yellow for Micro-Blocks, Cyan for Structures
            Color c = isBlockMode ? Color.yellow : Color.cyan;

            // Validation Logic for Micro-Blocks
            if (isBlockMode)
            {
                BoundsInt b = GetSelectionBounds(pointA.Value, p2);
                bool isCubic = (b.size.x == b.size.y && b.size.x == b.size.z);
                bool isSupported = (b.size.x == 8 || b.size.x == 16 || b.size.x == 32);
                if (!isCubic || !isSupported) c = Color.red; // Invalid selection
            }

            DrawSelectionBox(pointA.Value, p2, c);

            // 'G' to Open Save Dialog
            if (Input.GetKeyDown(KeyCode.G))
            {
                // Set default names based on context
                if (isBlockMode)
                {
                    BoundsInt b = GetSelectionBounds(pointA.Value, p2);
                    if (b.size.x == b.size.y && b.size.x == b.size.z && (b.size.x == 8 || b.size.x == 16 || b.size.x == 32))
                    {
                        saveName = $"Block_{b.size.x}x";
                        isTypingName = true;
                    }
                    else
                    {
                        Debug.LogError("Micro-Block selection must be cubic (8x8x8, 16x16x16, etc)");
                    }
                }
                else
                {
                    saveName = "NewStructure";
                    isTypingName = true;
                }
            }
        }
    }

    void PerformSave()
    {
        if (pointA.HasValue && pointB.HasValue)
        {
            BoundsInt bounds = GetSelectionBounds(pointA.Value, pointB.Value);

            if (isBlockMode)
            {
                CaptureMicroModel(bounds.min, bounds.size.x, saveName);
            }
            else
            {
                // THE NEW ROBUST STRUCTURE SAVER
                CaptureStructure(bounds, saveName);
            }
        }
        else
        {
            Debug.LogWarning("Cannot save: No selection made.");
        }

        isTypingName = false;
        ctrl.movement.InputLocked = false;
        ClearSelection();
    }

    // --- STRUCTURE SAVING (ROBUST) ---
    void CaptureStructure(BoundsInt bounds, string filename)
    {
#if UNITY_EDITOR
        VoxelStructure structData = new VoxelStructure();
        structData.structureName = filename;
        structData.blocks = new List<VoxelBlockEntry>();

        // 1. Pivot Calculation (Bottom Center)
        Vector3Int min = bounds.min;
        Vector3Int pivot = new Vector3Int(min.x + (bounds.size.x / 2), min.y, min.z + (bounds.size.z / 2));

        int count = 0;

        // 2. Scan Loop
        for (int x = min.x; x < min.x + bounds.size.x; x++)
        {
            for (int y = min.y; y < min.y + bounds.size.y; y++)
            {
                for (int z = min.z; z < min.z + bounds.size.z; z++)
                {
                    BlockData b = ctrl.world.GetBlock(new Vector3(x, y, z));
                    if (b != null)
                    {
                        // Store relative coordinates
                        structData.blocks.Add(new VoxelBlockEntry(x - pivot.x, y - pivot.y, z - pivot.z, b.blockName));
                        count++;
                    }
                }
            }
        }

        if (count == 0)
        {
            Debug.LogWarning("Selection was empty.");
            return;
        }

        // 3. File System Setup
        string jsonFolder = "Assets/Resources/Structures/Generated/JSON";
        string assetFolder = "Assets/Resources/Structures/Generated";

        if (!Directory.Exists(jsonFolder)) Directory.CreateDirectory(jsonFolder);
        if (!Directory.Exists(assetFolder)) Directory.CreateDirectory(assetFolder);

        string jsonPath = $"{jsonFolder}/{filename}.json";
        string assetPath = $"{assetFolder}/{filename}.asset";

        // 4. Write JSON
        string jsonContent = JsonUtility.ToJson(structData, true);
        File.WriteAllText(jsonPath, jsonContent);

        // 5. FORCE IMPORT (Fixes the "Restart Required" bug)
        AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceUpdate);

        // 6. Link to Scriptable Object
        TextAsset importedJson = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
        if (importedJson == null)
        {
            Debug.LogError("Critical Error: JSON saved but could not be loaded immediately.");
            return;
        }

        StructureDataSO so = ScriptableObject.CreateInstance<StructureDataSO>();
        so.structureJson = importedJson;
        so.spawnDensity = 1;
        so.spawnRadius = 3; // Default footprint

        // 7. Save Asset
        AssetDatabase.CreateAsset(so, assetPath);
        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>SUCCESS: Structure '{filename}' saved! ({count} blocks)</color>");
        EditorGUIUtility.PingObject(so);
#else
        Debug.LogError("Saving structures is only supported in the Unity Editor.");
#endif
    }

    // --- MICRO-BLOCK SAVING (Legacy Logic) ---
    void CaptureMicroModel(Vector3Int start, int res, string filename)
    {
        MicroModelData data = new MicroModelData();
        data.resolution = res;
        data.voxels = new Color32[res * res * res];

        for (int x = 0; x < res; x++)
        {
            for (int y = 0; y < res; y++)
            {
                for (int z = 0; z < res; z++)
                {
                    BlockData b = ctrl.world.GetBlock(new Vector3(start.x + x, start.y + y, start.z + z));
                    int index = x + (y * res) + (z * res * res);

                    if (b != null) data.voxels[index] = b.blockColor;
                    else data.voxels[index] = new Color32(0, 0, 0, 0);
                }
            }
        }

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

    // --- HELPERS ---
    void HandleTypingInput()
    {
        ctrl.movement.InputLocked = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) PerformSave();
        if (Input.GetKeyDown(KeyCode.Escape)) { isTypingName = false; ctrl.movement.InputLocked = false; }
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

        // Draw Wireframe
        Debug.DrawLine(min, new Vector3(max.x, min.y, min.z), c);
        Debug.DrawLine(min, new Vector3(min.x, max.y, min.z), c);
        Debug.DrawLine(min, new Vector3(min.x, min.y, max.z), c);

        Debug.DrawLine(max, new Vector3(min.x, max.y, max.z), c);
        Debug.DrawLine(max, new Vector3(max.x, min.y, max.z), c);
        Debug.DrawLine(max, new Vector3(max.x, max.y, min.z), c);

        Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), c);
        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), c);
        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), c);
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

        string mode = isBlockMode ? "MICRO-BLOCK" : "STRUCTURE";
        string info = "Select Area [L/R] | [G] Save | [Tab] Switch";

        if (isBlockMode && pointA.HasValue)
        {
            Vector3Int p2 = pointB.HasValue ? pointB.Value : currentCursor;
            BoundsInt b = GetSelectionBounds(pointA.Value, p2);
            bool ok = (b.size.x == b.size.y && b.size.x == b.size.z) && (b.size.x == 8 || b.size.x == 16);
            string c = ok ? "green" : "red";
            info = $"Size: <color={c}>{b.size.x}</color> (Must be 8 or 16)";
        }

        GUI.Label(new Rect(20, Screen.height - 70, 400, 30), $"<b>{mode} MODE</b>");
        GUI.Label(new Rect(20, Screen.height - 50, 400, 30), info);
    }
}