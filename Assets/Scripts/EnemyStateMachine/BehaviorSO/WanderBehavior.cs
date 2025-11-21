using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Behaviours/Grid Wander Logic")]
public class GridWanderBehaviour : PawnStateBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2.0f;
    public float waitTime = 1.0f;
    public int wanderRadius = 5;

    [Header("Grid Alignment")]
    [Tooltip("Add 0.5 to X/Z if you want to stand between integer grid lines. Keep 0 for standard cubes.")]
    public Vector3 gridOffset = Vector3.zero;

    [Tooltip("Height adjustment. 0.5 sits on top of a 1x1 cube.")]
    public float verticalOffset = 0.5f;

    [Header("Physics")]
    public LayerMask obstacleMask;
    public LayerMask groundMask;

    // Runtime
    private bool isWaiting;
    private Vector3 startOrigin;

    public override void Enter(PawnContext ctx)
    {
        Debug.Log($"[Pawn] ENTER Wander State on {ctx.gameObject.name}");

        // Stability Setup
        if (ctx.rb != null)
        {
            ctx.rb.isKinematic = true;
            ctx.rb.useGravity = false;
        }
        if (ctx.animator != null) ctx.animator.applyRootMotion = false;

        if (startOrigin == Vector3.zero) startOrigin = ctx.transform.position;

        isWaiting = false;
        PickNewTarget(ctx);
    }

    public override void Execute(PawnContext ctx)
    {
        if (isWaiting)
        {
            if (Time.time > ctx.stateTimer)
            {
                isWaiting = false;
                PickNewTarget(ctx);
            }
        }
        else
        {
            Move(ctx);
        }
    }

    public override void Exit(PawnContext ctx)
    {
        ctx.transform.position = ctx.currentGridTarget;
    }

    private void Move(PawnContext ctx)
    {
        Vector3 current = ctx.transform.position;
        Vector3 target = ctx.currentGridTarget;

        float step = moveSpeed * Time.deltaTime;
        // Safety clamp to prevent teleporting
        if (step > 0.5f) step = 0.5f;

        ctx.transform.position = Vector3.MoveTowards(current, target, step);

        // Rotation Logic
        Vector3 dir = (target - current);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            ctx.transform.rotation = Quaternion.Slerp(ctx.transform.rotation, rot, 10f * Time.deltaTime);
        }

        // Arrived?
        if (Vector3.Distance(ctx.transform.position, target) < 0.01f)
        {
            ctx.transform.position = target;
            isWaiting = true;
            ctx.stateTimer = Time.time + waitTime;
        }
    }

    private void PickNewTarget(PawnContext ctx)
    {
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        for (int i = 0; i < 10; i++)
        {
            Vector3 dir = dirs[Random.Range(0, dirs.Length)];

            // 1. Calculate Base Position (Integer Snapped + User Offset)
            Vector3 currentSnapped = GetGridCoordinate(ctx.transform.position);
            Vector3 flatCandidate = GetGridCoordinate(currentSnapped + dir);

            // 2. Check Elevations (Flat -> Up -> Down)

            // A. Flat
            if (IsValid(flatCandidate)) { SetTarget(ctx, flatCandidate); return; }

            // B. Step Up (+1 Y)
            Vector3 upCandidate = flatCandidate + Vector3.up;
            if (IsValid(upCandidate)) { SetTarget(ctx, upCandidate); return; }

            // C. Step Down (-1 Y)
            Vector3 downCandidate = flatCandidate + Vector3.down;
            if (IsValid(downCandidate)) { SetTarget(ctx, downCandidate); return; }
        }

        // Use Error log so it is visible even if warnings are hidden
        Debug.LogError($"[Pawn] FAILED to find target from {ctx.transform.position}. Check GroundMask or Offsets.");
        isWaiting = true;
        ctx.stateTimer = Time.time + 1.0f;
    }

    private void SetTarget(PawnContext ctx, Vector3 t)
    {
        ctx.currentGridTarget = t;
        // Debug.DrawLine(ctx.transform.position, t, Color.green, 2.0f);
    }

    private Vector3 GetGridCoordinate(Vector3 rawPos)
    {
        // Integer Snapping
        float x = Mathf.Round(rawPos.x);
        float z = Mathf.Round(rawPos.z);

        // Vertical snapping (Integer + visual offset)
        // We subtract the offset, round, then add it back to stay consistent
        float y = Mathf.Round(rawPos.y - verticalOffset) + verticalOffset;

        return new Vector3(x, y, z) + gridOffset;
    }

    private bool IsValid(Vector3 target)
    {
        // 1. Leash
        if (Vector3.Distance(target, startOrigin) > wanderRadius) return false;

        // 2. Head Check (Blocked?)
        // Check 0.25 units above the floor target (Ankle/Shin level)
        if (Physics.CheckSphere(target + Vector3.up * 0.25f, 0.3f, obstacleMask)) return false;

        // 3. Foot Check (Is there ground?)
        // Raycast from slightly above (0.1) downwards.
        // Length 0.5 should hit the block immediately below.
        if (!Physics.Raycast(target + Vector3.up * 0.1f, Vector3.down, 0.5f, groundMask))
        {
            // Debug.DrawRay(target + Vector3.up * 0.1f, Vector3.down * 0.5f, Color.red, 1.0f);
            return false;
        }

        return true;
    }
}