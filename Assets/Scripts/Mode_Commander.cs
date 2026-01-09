using UnityEngine;

public class Mode_Commander : IGameMode
{
    private GameModeController ctrl;
    private PawnStateMachine selectedPawn;
    private int pawnLayerMask;

    public string ModeName => "Commander";

    public Mode_Commander(GameModeController c)
    {
        ctrl = c;
        int layerIndex = LayerMask.NameToLayer("Pawn");
        pawnLayerMask = (layerIndex != -1) ? (1 << layerIndex) : -1;
    }

    public void SetupMovement(PlayerMovement move) { }

    public void OnEnter() { }
    public void OnExit() { selectedPawn = null; }

    public void OnUpdate(Ray ray)
    {
        // 1. VISUALIZATION (Hover Logic)
        if (selectedPawn != null)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, ctrl.interactionRange, ctrl.interactionMask))
            {
                // Check if holding CTRL
                bool isBreakMode = Input.GetKey(KeyCode.LeftControl);

                Color color = isBreakMode ? Color.red : Color.green;
                // Draw a marker so you know where you are commanding
                Debug.DrawLine(hit.point, hit.point + Vector3.up, color);
            }
        }

        // 2. SELECT PAWN (Right Click)
        if (Input.GetMouseButtonDown(1))
        {
            if (Physics.Raycast(ray, out RaycastHit hitPawn, ctrl.interactionRange, pawnLayerMask))
            {
                var psm = hitPawn.collider.GetComponent<PawnStateMachine>();
                if (psm != null)
                {
                    selectedPawn = psm;
                    Debug.Log($"<color=cyan>Selected Unit: {psm.name}</color>");
                    return; // Stop here
                }
            }
        }

        // 3. ISSUE COMMAND (Right Click + Unit Selected)
        if (Input.GetMouseButtonDown(1) && selectedPawn != null)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, ctrl.interactionRange, ctrl.interactionMask))
            {
                // DETECT INTENT
                bool isBreakOrder = Input.GetKey(KeyCode.LeftControl);
                Vector3 target;
                OrderType type;

                if (isBreakOrder)
                {
                    // Target INSIDE the block
                    target = hit.point - (hit.normal * 0.1f);
                    type = OrderType.Break;
                }
                else
                {
                    // Target OUTSIDE the block (The surface)
                    target = hit.point + (hit.normal * 0.1f);
                    type = OrderType.Move;
                }

                Vector3Int gridTarget = new Vector3Int(
                    Mathf.FloorToInt(target.x),
                    Mathf.FloorToInt(target.y),
                    Mathf.FloorToInt(target.z)
                );

                Debug.Log($"<color={(isBreakOrder ? "red" : "green")}>Sending {type} Order to {gridTarget}</color>");

                if (Input.GetKey(KeyCode.LeftShift)) selectedPawn.QueueCommand(gridTarget, type);
                else
                {
                    selectedPawn.ClearCommands();
                    selectedPawn.QueueCommand(gridTarget, type);
                }
            }
        }

        // 4. DESELECT
        if (Input.GetMouseButtonDown(0)) selectedPawn = null;
    }

    public void OnGUI()
    {
        string pName = selectedPawn != null ? selectedPawn.name : "None";
        GUI.Label(new Rect(20, Screen.height - 50, 400, 30), $"Unit: <color=cyan>{pName}</color>");

        string instructions = "[R-Click: Select/Move] [Ctrl+R: Break] [Shift: Queue]";
        // Highlight instructions if Ctrl is held
        if (Input.GetKey(KeyCode.LeftControl)) instructions = "<color=red>[R-Click: BREAK ORDER]</color>";

        GUI.Label(new Rect(20, Screen.height - 30, 400, 30), instructions);
    }
}