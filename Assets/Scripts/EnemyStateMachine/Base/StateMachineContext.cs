using UnityEngine;

public class PawnContext
{
    // References
    public Transform transform;
    public GameObject gameObject;
    public Rigidbody rb;
    public Animator animator;
    public VoxelWorld world;
    public PawnIdentity identity; // <-- NEW: Self Reference

    // Runtime Data
    public Vector3 currentGridTarget;
    public float stateTimer;
    public float lastTransitionTime;

    // Sensor Cache (Optimization)
    private float lastScanTime;
    private Transform cachedTarget;
    private const float SCAN_INTERVAL = 0.5f; // Only scan twice per second

    public PawnContext(Transform t, Rigidbody r, VoxelWorld w, Animator a, PawnIdentity id)
    {
        transform = t;
        gameObject = t.gameObject;
        rb = r;
        world = w;
        animator = a;
        identity = id;
    }

    // BEST PRACTICE: Throttled Sensor Scan
    // Scans for the closest pawn of a specific type.
    public Transform ScanForTarget(PawnType searchType)
    {
        // Return cached result if we scanned recently
        if (Time.time - lastScanTime < SCAN_INTERVAL && cachedTarget != null)
        {
            // Verify cache is still valid/active
            if (cachedTarget.gameObject.activeInHierarchy)
                return cachedTarget;
        }

        lastScanTime = Time.time;
        cachedTarget = null;

        // Physics Overlap is okay here because we throttle it
        Collider[] hits = Physics.OverlapSphere(transform.position, identity.sightRadius);
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue; // Ignore self

            var otherPawn = hit.GetComponent<PawnIdentity>();
            if (otherPawn != null && otherPawn.type == searchType)
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