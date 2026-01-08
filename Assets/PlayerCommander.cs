using UnityEngine;

public class PlayerCommander : MonoBehaviour
{
    [Header("Settings")]
    public Camera playerCamera;
    public LayerMask pawnLayer;
    public LayerMask terrainLayer;
    public float interactionRange = 100f;

    [Header("Debug")]
    public PawnStateMachine selectedPawn;

    void Update()
    {
        // 1. SELECT PAWN (Look + E)
        if (Input.GetKeyDown(KeyCode.E))
        {
            AttemptSelectPawn();
        }

        // 2. QUEUE COMMAND (Left Click)
        if (Input.GetMouseButtonDown(0))
        {
            AttemptQueueCommand();
        }

        // 3. CLEAR COMMANDS (Right Click - Optional)
        if (Input.GetMouseButtonDown(1) && selectedPawn != null)
        {
            selectedPawn.ClearCommands();
            Debug.Log("Commands Cleared");
        }
    }

    void AttemptSelectPawn()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, pawnLayer))
        {
            PawnStateMachine psm = hit.collider.GetComponent<PawnStateMachine>();
            if (psm != null)
            {
                selectedPawn = psm;
                Debug.Log($"Selected Pawn: {psm.name}");
            }
        }
    }

    void AttemptQueueCommand()
    {
        if (selectedPawn == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, terrainLayer))
        {
            // Snap to grid center
            Vector3 target = new Vector3(
                Mathf.Floor(hit.point.x) + 0.5f,
                Mathf.Floor(hit.point.y) + 1.0f, // +1.0f to sit on top of the block
                Mathf.Floor(hit.point.z) + 0.5f
            );

            selectedPawn.QueueCommand(target);

            // Visual Debug
            Debug.DrawLine(hit.point, hit.point + Vector3.up * 2, Color.green, 1.0f);
        }
    }

    // Draw visual line to selected pawn
    void OnDrawGizmos()
    {
        if (selectedPawn != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(selectedPawn.transform.position, selectedPawn.transform.position + Vector3.up * 2.5f);
        }
    }
}