using UnityEngine;
using System.IO;

public class Mode_Scanner : IGameMode
{
    private GameModeController ctrl;
    private Vector3Int? pointA;
    private Vector3Int? pointB;

    public string ModeName => "Scanner";

    public Mode_Scanner(GameModeController c) { ctrl = c; }
    public void SetupMovement(PlayerMovement move) { }
    public void OnEnter() { }
    public void OnExit() { pointA = null; pointB = null; }

    public void OnUpdate(Ray ray)
    {
        if (pointA.HasValue)
        {
            Vector3 pA = (Vector3)pointA.Value;
            Vector3 pB = pointB.HasValue ? (Vector3)pointB.Value : ray.GetPoint(10);
            Debug.DrawLine(pA, pB, Color.red);
        }

        if (Physics.Raycast(ray, out RaycastHit hit, ctrl.interactionRange, ctrl.interactionMask))
        {
            Vector3 p = hit.point - (hit.normal * 0.1f);
            Vector3Int pos = new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));

            if (Input.GetMouseButtonDown(0)) pointA = pos; // L-Click
            if (Input.GetMouseButtonDown(1)) pointB = pos; // R-Click
        }

        // Changed from BackQuote to C to avoid conflict
        if (Input.GetKeyDown(KeyCode.C))
        {
            pointA = null;
            pointB = null;
            Debug.Log("Selection Cleared.");
        }

        if (Input.GetKeyDown(KeyCode.G) && pointA.HasValue && pointB.HasValue)
        {
            Debug.Log($"Saving Structure...");
            // SaveStructure();
        }
    }

    public void OnGUI()
    {
        string status = "Select A (L-Click)";
        if (pointA.HasValue) status = "Select B (R-Click) | Press C to Clear";
        if (pointA.HasValue && pointB.HasValue) status = "Press G to Save";
        GUI.Label(new Rect(20, Screen.height - 50, 300, 30), status);
    }
}