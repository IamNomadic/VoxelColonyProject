using UnityEngine;

public class PawnContext
{
    // References
    public Transform transform;
    public GameObject gameObject;
    public Rigidbody rb;
    public Animator animator;
    public VoxelWorld world;

    // THE NEW DATA SOURCE
    public PawnDataSO data;

    // Runtime Data
    public Vector3 currentGridTarget;
    public float stateTimer;
    public float lastTransitionTime;

    // Sensor Cache
    private float lastScanTime;
    private Transform cachedTarget;
    private const float SCAN_INTERVAL = 0.5f;

    public PawnContext(Transform t, Rigidbody r, VoxelWorld w, Animator a, PawnDataSO d)
    {
        transform = t;
        gameObject = t.gameObject;
        rb = r;
        world = w;
        animator = a;
        data = d;
    }

    // --- UPDATED SENSOR LOGIC ---
    public void SetTargetAndSnap(Vector3 rawPosition)
    {
        float ix = Mathf.Floor(rawPosition.x);
        float iy = Mathf.Floor(rawPosition.y);
        float iz = Mathf.Floor(rawPosition.z);

        // Read offset from DATA now
        currentGridTarget = new Vector3(
            ix + 0.5f,
            iy + data.verticalOffset,
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

        // Scan using data.sightRadius
        Collider[] hits = Physics.OverlapSphere(transform.position, data.sightRadius);
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            // FIX: We now look for the StateMachine, because PawnIdentity is gone
            var otherPawn = hit.GetComponent<PawnStateMachine>();

            // Access data through the StateMachine
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