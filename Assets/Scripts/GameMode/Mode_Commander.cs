using UnityEngine;
using System.Collections.Generic;

public class Mode_Commander : IGameMode
{
    private GameModeController ctrl;

    // Multi-Selection State
    private List<Pawn> selectedPawns = new List<Pawn>();

    // State Machine
    private enum CommanderState { Normal, Menu, Targeting }
    private CommanderState currentState = CommanderState.Normal;

    // Normal State - Box Selection
    private bool isDraggingSelect = false;
    private Vector3 mouseSelectStart;

    // Targeting State
    private JobDef pendingJobDef = JobDef.None;
    private bool isDraggingOrder = false;
    private Vector3Int orderGridStart;
    private Vector3Int orderGridEnd;
    private int nextBuildPlanId;
    private bool hasTargetPreview = false;
    private Vector3Int targetPreviewStart;
    private Vector3Int targetPreviewEnd;

    public string ModeName => "Commander";

    public Mode_Commander(GameModeController c)
    {
        ctrl = c;
    }

    public void SetupMovement(PlayerMovement move)
    {
        move.SetFlying(true);
        move.obeysSimulationClock = false;
    }

    public void OnEnter()
    {
        selectedPawns.Clear();
        currentState = CommanderState.Normal;
    }

    public void OnExit()
    {
        selectedPawns.Clear();
        currentState = CommanderState.Normal;
    }

    public void OnUpdate(Ray ray)
    {
        // 1. HIGHLIGHT SELECTED PAWNS
        foreach (var pawn in selectedPawns)
        {
            if (pawn != null && !pawn.IsPossessed)
            {
                Debug.DrawLine(pawn.transform.position, pawn.transform.position + Vector3.up * 1.5f, Color.cyan);
            }
        }

        switch (currentState)
        {
            case CommanderState.Normal:
                HandleNormalState(ray);
                break;
            case CommanderState.Menu:
                HandleMenuState();
                break;
            case CommanderState.Targeting:
                HandleTargetingState(ray);
                break;
        }
    }

    // ==========================================
    // STATE 1: NORMAL (Select, Move, Possess)
    // ==========================================
    private void HandleNormalState(Ray ray)
    {
        if (Input.GetKeyDown(KeyCode.F) && selectedPawns.Count == 1)
        {
            ctrl.PossessPawn(selectedPawns[0]);
            selectedPawns.Clear();
            return;
        }

        if (Input.GetKeyDown(KeyCode.G) && selectedPawns.Count > 0)
        {
            currentState = CommanderState.Menu;
            ctrl.movement.InputLocked = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        // LEFT CLICK: PAWN SELECTION
        if (Input.GetMouseButtonDown(0))
        {
            isDraggingSelect = true;
            mouseSelectStart = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0) && isDraggingSelect)
        {
            isDraggingSelect = false;
            ExecuteSelectionBox();
        }

        // RIGHT CLICK: IMMEDIATE MOVE ORDER
        if (Input.GetMouseButtonDown(1) && selectedPawns.Count > 0)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, ctrl.interactionRange, ctrl.interactionMask))
            {
                Vector3Int target = Vector3Int.FloorToInt(hit.point + (hit.normal * 0.05f));

                bool queueJob = Input.GetKey(KeyCode.LeftShift);
                if (!queueJob) foreach (var p in selectedPawns) p.JobTracker.ClearJobs();
                List<Vector3Int> moveTargets = GetMoveTargets(target, selectedPawns.Count);
                for (int i = 0; i < selectedPawns.Count && i < moveTargets.Count; i++)
                {
                    selectedPawns[i].JobTracker.QueueJob(new Job(JobDef.Move, moveTargets[i]));
                }

                Debug.Log($"Ordered {moveTargets.Count} pawns to MOVE near {target}");
            }
        }
    }

    private List<Vector3Int> GetMoveTargets(Vector3Int center, int count)
    {
        List<Vector3Int> targets = new List<Vector3Int>();
        VoxelWorld world = selectedPawns[0].World;
        int radius = 0;

        while (targets.Count < count && radius <= count * 2)
        {
            List<Vector3Int> ring = new List<Vector3Int>();
            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != radius) continue;
                    ring.Add(center + new Vector3Int(x, 0, z));
                }
            }

            ring.Sort((a, b) =>
            {
                int distanceA = Mathf.Abs(a.x - center.x) + Mathf.Abs(a.z - center.z);
                int distanceB = Mathf.Abs(b.x - center.x) + Mathf.Abs(b.z - center.z);
                return distanceA.CompareTo(distanceB);
            });

            foreach (Vector3Int candidate in ring)
            {
                if (targets.Count >= count) break;
                if (VoxelPathHelper.IsWalkable(world, candidate, checkPawns: false))
                {
                    targets.Add(candidate);
                }
            }

            radius++;
        }

        return targets;
    }

    private void ExecuteSelectionBox()
    {
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift);
        Vector3 mouseEnd = Input.mousePosition;
        Rect selectionRect = GetScreenRect(mouseSelectStart, mouseEnd);

        bool isClick = selectionRect.width < 10 && selectionRect.height < 10;

        if (isClick)
        {
            Ray ray = ctrl.mainCamera.ScreenPointToRay(mouseEnd);

            if (Physics.Raycast(ray, out RaycastHit hit, ctrl.interactionRange))
            {
                Pawn clickedPawn = hit.collider.GetComponentInParent<Pawn>();
                if (clickedPawn != null && !clickedPawn.IsPossessed)
                {
                    if (shiftHeld)
                    {
                        if (selectedPawns.Contains(clickedPawn)) selectedPawns.Remove(clickedPawn);
                        else selectedPawns.Add(clickedPawn);
                    }
                    else
                    {
                        selectedPawns.Clear();
                        selectedPawns.Add(clickedPawn);
                    }
                    return;
                }
            }

            if (!shiftHeld) selectedPawns.Clear();
        }
        else
        {
            if (!shiftHeld) selectedPawns.Clear();

            Pawn[] allPawns = Object.FindObjectsOfType<Pawn>();
            foreach (Pawn p in allPawns)
            {
                if (p.IsPossessed) continue;

                Vector3 screenPos = ctrl.mainCamera.WorldToScreenPoint(p.transform.position);
                if (screenPos.z > 0 && selectionRect.Contains(new Vector2(screenPos.x, Screen.height - screenPos.y)))
                {
                    if (!selectedPawns.Contains(p)) selectedPawns.Add(p);
                }
            }
        }
    }

    // ==========================================
    // STATE 2: MENU
    // ==========================================
    private void HandleMenuState()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.G))
        {
            CloseMenuAndReturnToNormal();
        }
    }

    private void CloseMenuOnly()
    {
        ctrl.movement.InputLocked = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void CloseMenuAndReturnToNormal()
    {
        currentState = CommanderState.Normal;
        CloseMenuOnly();
    }

    // ==========================================
    // STATE 3: TARGETING AREA
    // ==========================================
    private void HandleTargetingState(Ray ray)
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            isDraggingOrder = false;
            hasTargetPreview = false;
            currentState = CommanderState.Normal;
            return;
        }

        bool hitValid = Physics.Raycast(ray, out RaycastHit hit, ctrl.interactionRange, ctrl.interactionMask);
        Vector3Int currentHoverGrid = Vector3Int.zero;

        if (hitValid)
        {
            currentHoverGrid = GetTargetGrid(hit, pendingJobDef);
            targetPreviewStart = isDraggingOrder ? orderGridStart : currentHoverGrid;
            targetPreviewEnd = currentHoverGrid;
            hasTargetPreview = true;

            if (!isDraggingOrder)
            {
                DrawBoundingBox(currentHoverGrid, currentHoverGrid, GetJobColor(pendingJobDef));
            }
        }

        if (Input.GetMouseButtonDown(0) && hitValid)
        {
            isDraggingOrder = true;
            orderGridStart = currentHoverGrid;
        }

        if (isDraggingOrder && hitValid)
        {
            orderGridEnd = currentHoverGrid;
            DrawBoundingBox(orderGridStart, orderGridEnd, GetJobColor(pendingJobDef));
        }

        if (!hitValid)
        {
            hasTargetPreview = false;
        }

        if (Input.GetMouseButtonUp(0) && isDraggingOrder)
        {
            isDraggingOrder = false;
            DispatchAreaOrder();
            hasTargetPreview = false;
        }
    }

    private Vector3Int GetTargetGrid(RaycastHit hit, JobDef def)
    {
        if (def == JobDef.Mine)
            return Vector3Int.FloorToInt(hit.point - (hit.normal * 0.05f));
        else
            return Vector3Int.FloorToInt(hit.point + (hit.normal * 0.05f));
    }

    private Color GetJobColor(JobDef def)
    {
        if (def == JobDef.Mine) return Color.red;
        if (def == JobDef.Place) return Color.yellow;
        return Color.green;
    }

    private void DispatchAreaOrder()
    {
        if (selectedPawns.Count == 0) return;

        bool queueJob = Input.GetKey(KeyCode.LeftShift);

        Vector3Int min = Vector3Int.Min(orderGridStart, orderGridEnd);
        Vector3Int max = Vector3Int.Max(orderGridStart, orderGridEnd);
        List<Vector3Int> targets = new List<Vector3Int>();

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    targets.Add(new Vector3Int(x, y, z));
                }
            }
        }

        if (targets.Count == 0) return;

        if (pendingJobDef == JobDef.Place)
        {
            targets.Sort((a, b) => CompareBuildTargets(a, b, min, max));
        }
        else
        {
            Vector3 pawnsCenter = Vector3.zero;
            foreach (var p in selectedPawns) pawnsCenter += p.transform.position;
            pawnsCenter /= selectedPawns.Count;
            targets.Sort((a, b) => Vector3.Distance(a, pawnsCenter).CompareTo(Vector3.Distance(b, pawnsCenter)));
        }

        if (!queueJob)
        {
            foreach (var p in selectedPawns) p.JobTracker.ClearJobs();
        }

        int pawnIndex = 0;
        int buildOrder = 0;
        int buildPlanId = pendingJobDef == JobDef.Place ? ++nextBuildPlanId : 0;
        foreach (Vector3Int target in targets)
        {
            Job newJob = new Job(pendingJobDef, target, null, buildOrder++, buildPlanId);
            selectedPawns[pawnIndex].JobTracker.QueueJob(newJob);

            pawnIndex++;
            if (pawnIndex >= selectedPawns.Count) pawnIndex = 0;
        }

        Debug.Log($"Issued {targets.Count} {pendingJobDef} orders to {selectedPawns.Count} pawns.");
    }

    private int CompareBuildTargets(Vector3Int a, Vector3Int b, Vector3Int min, Vector3Int max)
    {
        if (a.y != b.y) return a.y.CompareTo(b.y);

        bool aCorridor = IsAccessCorridor(a, min, max);
        bool bCorridor = IsAccessCorridor(b, min, max);
        if (aCorridor != bCorridor) return aCorridor ? 1 : -1;

        int aBoundaryDistance = GetBoundaryDistance(a, min, max);
        int bBoundaryDistance = GetBoundaryDistance(b, min, max);
        if (aBoundaryDistance != bBoundaryDistance)
        {
            return bBoundaryDistance.CompareTo(aBoundaryDistance);
        }

        if (a.x != b.x) return a.x.CompareTo(b.x);
        return a.z.CompareTo(b.z);
    }

    private bool IsAccessCorridor(Vector3Int position, Vector3Int min, Vector3Int max)
    {
        int width = max.x - min.x + 1;
        int depth = max.z - min.z + 1;
        bool hasCrossSection = width >= 3 && depth >= 3;
        if (!hasCrossSection) return false;

        return position.x == min.x + 1 || position.z == min.z + 1;
    }

    private int GetBoundaryDistance(Vector3Int position, Vector3Int min, Vector3Int max)
    {
        return Mathf.Min(
            Mathf.Min(position.x - min.x, max.x - position.x),
            Mathf.Min(position.z - min.z, max.z - position.z));
    }

    // ==========================================
    // GUI RENDERING
    // ==========================================
    public void OnGUI()
    {
        if (currentState == CommanderState.Menu)
        {
            DrawOrderMenu();
            return;
        }

        if (isDraggingSelect)
        {
            Rect rect = GetScreenRect(mouseSelectStart, Input.mousePosition);
            GUI.color = new Color(0, 1, 1, 0.2f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        if (currentState == CommanderState.Targeting && hasTargetPreview)
        {
            DrawTargetPreviewGUI(targetPreviewStart, targetPreviewEnd, GetJobColor(pendingJobDef));
        }

        string pName = selectedPawns.Count > 0 ? $"{selectedPawns.Count} Units Selected" : "None";
        GUI.Label(new Rect(20, Screen.height - 70, 400, 30), $"<b><color=cyan>{pName}</color></b>");

        if (currentState == CommanderState.Normal)
        {
            string instructions = "[L-Drag: Select] [Shift: Add] [R-Click: Move]";
            if (selectedPawns.Count > 0) instructions += " | <b>[G] ORDERS MENU</b>";
            if (selectedPawns.Count == 1) instructions += " | <b>[F] POSSESS</b>";

            GUI.Label(new Rect(20, Screen.height - 50, 600, 40), instructions);
        }
        else if (currentState == CommanderState.Targeting)
        {
            string act = pendingJobDef == JobDef.Mine ? "<color=red>MINE</color>" : $"<color=yellow>PLACE</color>";
            GUI.Label(new Rect(20, Screen.height - 50, 600, 40), $"[L-Drag: SELECT {act} AREA] | [R-Click: Cancel] | [Shift: Queue]");
        }
    }

    private void DrawOrderMenu()
    {
        float w = 240;
        float h = 140;
        float cx = (Screen.width / 2f) - (w / 2f);
        float cy = (Screen.height / 2f) - (h / 2f);
        Rect menuRect = new Rect(cx, cy, w, h);

        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        GUI.DrawTexture(menuRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Box(menuRect, "<b>Select Order</b>");

        if (GUI.Button(new Rect(cx + 10, cy + 30, w - 20, 30), "Mine Area"))
        {
            pendingJobDef = JobDef.Mine;
            currentState = CommanderState.Targeting;
            CloseMenuOnly();
        }

        // Just a normal button now, no checks
        if (GUI.Button(new Rect(cx + 10, cy + 70, w - 20, 30), "Place Area"))
        {
            pendingJobDef = JobDef.Place;
            currentState = CommanderState.Targeting;
            CloseMenuOnly();
        }

        if (GUI.Button(new Rect(cx + 10, cy + 110, w - 20, 20), "Cancel"))
        {
            CloseMenuAndReturnToNormal();
        }
    }

    // ==========================================
    // UTILITIES
    // ==========================================
    private Rect GetScreenRect(Vector3 screenPosition1, Vector3 screenPosition2)
    {
        screenPosition1.y = Screen.height - screenPosition1.y;
        screenPosition2.y = Screen.height - screenPosition2.y;
        var topLeft = Vector3.Min(screenPosition1, screenPosition2);
        var bottomRight = Vector3.Max(screenPosition1, screenPosition2);
        return Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
    }

    private void DrawBoundingBox(Vector3Int start, Vector3Int end, Color color)
    {
        Vector3 min = Vector3Int.Min(start, end);
        Vector3 max = Vector3Int.Max(start, end) + Vector3.one;
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), color);
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), color);
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z), color);
        Debug.DrawLine(max, new Vector3(min.x, max.y, max.z), color);
        Debug.DrawLine(max, new Vector3(max.x, min.y, max.z), color);
        Debug.DrawLine(max, new Vector3(max.x, max.y, min.z), color);
        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), color);
        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), color);
        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), color);
        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), color);
        Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), color);
        Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), color);
    }

    private void DrawTargetPreviewGUI(Vector3Int start, Vector3Int end, Color color)
    {
        Vector3 min = Vector3Int.Min(start, end);
        Vector3 max = Vector3Int.Max(start, end) + Vector3.one;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z)
        };

        Vector2[] screenCorners = new Vector2[corners.Length];
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 screen = ctrl.mainCamera.WorldToScreenPoint(corners[i]);
            if (screen.z <= 0f) return;
            screenCorners[i] = new Vector2(screen.x, Screen.height - screen.y);
        }

        GUI.color = new Color(color.r, color.g, color.b, 0.95f);
        int[,] edges =
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
            { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
        };

        for (int i = 0; i < edges.GetLength(0); i++)
        {
            DrawScreenLine(screenCorners[edges[i, 0]], screenCorners[edges[i, 1]], 2f);
        }

        GUI.color = Color.white;
    }

    private void DrawScreenLine(Vector2 start, Vector2 end, float width)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        Matrix4x4 matrix = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one, start);
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, length, width), Texture2D.whiteTexture);
        GUI.matrix = matrix;
    }
}