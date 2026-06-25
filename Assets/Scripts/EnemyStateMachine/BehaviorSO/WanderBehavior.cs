using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Behaviours/Voxel Wander")]
public class VoxelWanderBehavior : PawnStateBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 2.0f;
    public float waitTime = 2.0f;
    public int wanderRadius = 8;

    // Runtime
    private bool isMoving;
    private Vector3 startPos;

    public override void Enter(PawnContext ctx)
    {
        if (ctx.rb != null) ctx.rb.isKinematic = true;
        if (startPos == Vector3.zero) startPos = ctx.transform.position;

        // Ensure we start with a snapped target
        ctx.SetTargetAndSnap(ctx.transform.position);
        isMoving = false;

        ctx.stateTimer = Time.time + Random.Range(0f, 1.0f);
        PickNewTarget(ctx);
    }

    public override void Execute(PawnContext ctx)
    {
        if (isMoving)
        {
            Move(ctx);
        }
        else
        {
            if (Time.time > ctx.stateTimer)
            {
                PickNewTarget(ctx);
            }
        }
    }

    public override void Exit(PawnContext ctx)
    {
        ctx.transform.position = ctx.currentGridTarget;
    }

    private void Move(PawnContext ctx)
    {
        ctx.transform.position = Vector3.MoveTowards(
            ctx.transform.position,
            ctx.currentGridTarget,
            moveSpeed * Time.deltaTime
        );

        Vector3 dir = ctx.currentGridTarget - ctx.transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            ctx.transform.rotation = Quaternion.Slerp(ctx.transform.rotation, rot, 5f * Time.deltaTime);
        }

        if (Vector3.Distance(ctx.transform.position, ctx.currentGridTarget) < 0.01f)
        {
            ctx.transform.position = ctx.currentGridTarget;
            isMoving = false;
            float variance = Random.Range(0.8f, 1.2f);
            ctx.stateTimer = Time.time + (waitTime * variance);
        }
    }

    private void PickNewTarget(PawnContext ctx)
    {
        Vector3 currentPos = ctx.transform.position;
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        // Shuffle
        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 temp = directions[i];
            int r = Random.Range(i, directions.Length);
            directions[i] = directions[r];
            directions[r] = temp;
        }

        foreach (var dir in directions)
        {
            Vector3 candidate = currentPos + dir;
            if (CheckAndSet(ctx, candidate)) return;
            if (CheckAndSet(ctx, candidate + Vector3.up)) return;
            if (CheckAndSet(ctx, candidate + Vector3.down)) return;
        }

        isMoving = false;
        ctx.stateTimer = Time.time + Random.Range(0.5f, 1.0f);
    }

    private bool CheckAndSet(PawnContext ctx, Vector3 target)
    {
        if (Vector3.Distance(target, startPos) > wanderRadius) return false;

        if (VoxelPathHelper.IsWalkable(ctx.world, target))
        {
            // USE THE FIX
            ctx.SetTargetAndSnap(target);
            isMoving = true;
            return true;
        }
        return false;
    }
}