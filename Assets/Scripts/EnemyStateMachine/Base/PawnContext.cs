using UnityEngine;
using System.Collections.Generic; // Required for Queue
public enum OrderType { Move, Break }

public struct PawnOrder
{
    public Vector3 target;
    public OrderType type;
}

public class PawnContext
{
    // References
    public Transform transform;
    public GameObject gameObject;
    public Rigidbody rb;
    public Animator animator;
    public VoxelWorld world;

    public PawnDataSO data;

    // Runtime Data
    public Vector3 currentGridTarget;
    public float stateTimer;
    public float lastTransitionTime;

    // --- NEW: COMMAND QUEUE SYSTEM ---
    // The target currently being pursued
    public Vector3? currentCommandTarget = null;
    // The list of future targets
    public Queue<Vector3> commandQueue = new Queue<Vector3>();

    // Sensor Cache
    private float lastScanTime;
    private Transform cachedTarget;
    private const float SCAN_INTERVAL = 0.5f;
  
    // Inside PawnContext class...
    public PawnOrder? currentOrder = null;
    public Queue<PawnOrder> orderQueue = new Queue<PawnOrder>();
    public PawnContext(Transform t, Rigidbody r, VoxelWorld w, Animator a, PawnDataSO d)
    {
        transform = t;
        gameObject = t.gameObject;
        rb = r;
        world = w;
        animator = a;
        data = d;

        // Initialize Queue
        commandQueue = new Queue<Vector3>();
    }

    public void SetTargetAndSnap(Vector3 rawPosition)
    {
        float ix = Mathf.Floor(rawPosition.x);
        float iy = Mathf.Floor(rawPosition.y);
        float iz = Mathf.Floor(rawPosition.z);

        currentGridTarget = new Vector3(
            ix + 0.5f,
            iy + data.verticalOffset, // Uses PawnDataSO
            iz + 0.5f
        );
    }

    public Transform ScanForTarget(PawnType searchType)
    {
        if (Time.time - lastScanTime < SCAN_INTERVAL && cachedTarget != null)
        {
            if (cachedTarget.gameObject.activeInHierarchy) return cachedTarget;
        }

        lastScanTime = Time.time;
        cachedTarget = null;

        Collider[] hits = Physics.OverlapSphere(transform.position, data.sightRadius); // Uses PawnDataSO
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            var otherPawn = hit.GetComponent<PawnStateMachine>();
            if (otherPawn != null && otherPawn.Data != null && otherPawn.Data.type == searchType)
            {
                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    cachedTarget = hit.transform;
                }
            }
        }
        return cachedTarget;
    }
}