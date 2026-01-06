using UnityEngine;

public class PawnContext
{
    // References
    public Transform transform;
    public GameObject gameObject;
    public Rigidbody rb;
    public Animator animator;
    public VoxelWorld world;
    public PawnIdentity identity;

    // Runtime Data
    public Vector3 currentGridTarget;
    public float stateTimer;
    public float lastTransitionTime;

    // Sensor Cache
    private float lastScanTime;
    private Transform cachedTarget;
    private const float SCAN_INTERVAL = 0.5f;

    public PawnContext(Transform t, Rigidbody r, VoxelWorld w, Animator a, PawnIdentity id)
    {
        transform = t;
        gameObject = t.gameObject;
        rb = r;
        world = w;
        animator = a;
        identity = id;
    }

    // --- THE FIX: ROBUST SETTER ---
    // Instead of setting currentGridTarget directly, use this helper.
    // It strips out any floating point errors and re-applies the exact Identity offset.
    public void SetTargetAndSnap(Vector3 rawPosition)
    {
        // 1. Integer Floor the coordinates to find the Block Index
        float ix = Mathf.Floor(rawPosition.x);
        float iy = Mathf.Floor(rawPosition.y);
        float iz = Mathf.Floor(rawPosition.z);

        // 2. Re-apply the perfect offsets
        // X and Z get +0.5 to be in the center of the block.
        // Y gets the verticalOffset from the Identity script.
        currentGridTarget = new Vector3(
            ix + 0.5f,
            iy + identity.verticalOffset,
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

        Collider[] hits = Physics.OverlapSphere(transform.position, identity.sightRadius);
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

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