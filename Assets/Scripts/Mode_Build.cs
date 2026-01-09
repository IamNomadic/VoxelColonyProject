using UnityEngine;

public class Mode_Builder : IGameMode
{
    private GameModeController ctrl;
    private int slotIndex = 0;

    public string ModeName => "Builder";

    public Mode_Builder(GameModeController c) { ctrl = c; }

    public void SetupMovement(PlayerMovement move)
    {
        // Example: If you wanted Builder to be slower
        // move.moveSpeed = 8f;
    }

    public void OnEnter() { }
    public void OnExit() { }

    public void OnUpdate(Ray ray)
    {
        // Hotbar keys
        if (Input.GetKeyDown(KeyCode.Alpha1)) slotIndex = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) slotIndex = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) slotIndex = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) slotIndex = 3;
        if (Input.GetKeyDown(KeyCode.Alpha5)) slotIndex = 4;
        if (Cursor.visible) return;
        // Interaction
        if (Physics.Raycast(ray, out RaycastHit hit, ctrl.interactionRange, ctrl.interactionMask))
        {
            if (Input.GetMouseButtonDown(0)) // Left: Break
            {
                ctrl.world.ModifyBlock(hit.point - (hit.normal * 0.1f), null);
            }
            if (Input.GetMouseButtonDown(1)) // Right: Place
            {
                BlockData b = ctrl.sharedHotbar[slotIndex];
                if (b != null)
                {
                    ctrl.world.ModifyBlock(hit.point + (hit.normal * 0.1f), b);
                }
            }
        }
    }

    public void OnGUI()
    {
        string bName = ctrl.sharedHotbar[slotIndex] != null ? ctrl.sharedHotbar[slotIndex].blockName : "Empty";
        GUI.Label(new Rect(20, Screen.height - 50, 300, 30), $"[L: Break] [R: Place] | Slot {slotIndex + 1}: {bName}");
    }
}