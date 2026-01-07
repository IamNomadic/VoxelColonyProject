using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Behaviours/Voxel Chase")]
public class VoxelChaseState : PawnStateBehaviour
{
    [Tooltip("Which species do we chase?")]
    public PawnType preyType = PawnType.Prey;

    // We now multiply the base speed from the Data Profile
    public float speedMultiplier = 1.2f;

    public override void Enter(PawnContext ctx) { }
    public override void Exit(PawnContext ctx) { }

    public override void Execute(PawnContext ctx)
    {
        // 1. Find Target
        Transform prey = ctx.ScanForTarget(preyType);

        // If target lost, do nothing (Transition will handle switching back to Wander)
        if (prey == null) return;

        // 2. Move towards Grid Target
        if (Vector3.Distance(ctx.transform.position, ctx.currentGridTarget) > 0.05f)
        {
            float huntSpeed = ctx.data.gravitySpeed * 0.3f * speedMultiplier; // Derive speed from Data

            ctx.transform.position = Vector3.MoveTowards(
                ctx.transform.position,
                ctx.currentGridTarget,
                huntSpeed * Time.deltaTime
            );

            // Look at target
            Vector3 moveDir = (ctx.currentGridTarget - ctx.transform.position);
            moveDir.y = 0;
            if (moveDir.sqrMagnitude > 0.05f)
            {
                Quaternion rot = Quaternion.LookRotation(moveDir);
                ctx.transform.rotation = Quaternion.Slerp(ctx.transform.rotation, rot, 10f * Time.deltaTime);
            }
            return;
        }

        // 3. Plan Next Step
        // Standard "Greedy Best-First" pathfinding towards prey
        Vector3 bestDir = VoxelPathHelper.GetCardinalDirection(ctx.transform.position, prey.position);

        if (TryMove(ctx, bestDir)) return;

        // If blocked, try sideways
        Vector3 altDir = new Vector3(bestDir.z, 0, bestDir.x);
        if (TryMove(ctx, altDir)) return;
        if (TryMove(ctx, -altDir)) return;
    }

    private bool TryMove(PawnContext ctx, Vector3 dir)
    {
        Vector3 target = ctx.transform.position + dir;

        // Check Flat, Up, and Down
        if (CheckPos(ctx, target)) return true;
        if (CheckPos(ctx, target + Vector3.up)) return true;
        if (CheckPos(ctx, target + Vector3.down)) return true;

        return false;
    }

    private bool CheckPos(PawnContext ctx, Vector3 target)
    {
        if (VoxelPathHelper.IsWalkable(ctx.world, target))
        {
            ctx.SetTargetAndSnap(target);
            return true;
        }
        return false;
    }
}