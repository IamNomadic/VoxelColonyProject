using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Behaviours/Voxel Flee")]
public class VoxelFleeState : PawnStateBehaviour
{
    [Tooltip("What are we running from?")]
    public PawnType dangerType = PawnType.Predator;
    public float speedMultiplier = 1.5f;

    [Tooltip("How many blocks down can the prey jump?")]
    public int maxJumpDrop = 4;

    public override void Enter(PawnContext ctx) { }
    public override void Exit(PawnContext ctx) { }

    public override void Execute(PawnContext ctx)
    {
        Transform threat = ctx.ScanForTarget(dangerType);
        if (threat == null) return;

        // 1. Execute Movement
        if (Vector3.Distance(ctx.transform.position, ctx.currentGridTarget) > 0.05f)
        {
            float panicSpeed = ctx.data.gravitySpeed * 0.3f * speedMultiplier;

            ctx.transform.position = Vector3.MoveTowards(
                ctx.transform.position,
                ctx.currentGridTarget,
                panicSpeed * Time.deltaTime
            );

            // Look rotation
            Vector3 moveDir = (ctx.currentGridTarget - ctx.transform.position);
            moveDir.y = 0;
            if (moveDir.sqrMagnitude > 0.05f)
            {
                Quaternion rot = Quaternion.LookRotation(moveDir);
                ctx.transform.rotation = Quaternion.Slerp(ctx.transform.rotation, rot, 15f * Time.deltaTime);
            }
            return;
        }

        // 2. Determine Flee Direction
        Vector3 dirToThreat = threat.position - ctx.transform.position;
        Vector3 dirAway = -dirToThreat;

        // Prioritize the dominant axis away from danger
        Vector3 bestDir = Vector3.zero;
        if (Mathf.Abs(dirAway.x) > Mathf.Abs(dirAway.z))
            bestDir = new Vector3(Mathf.Sign(dirAway.x), 0, 0);
        else
            bestDir = new Vector3(0, 0, Mathf.Sign(dirAway.z));

        // 3. Try Directions
        // A. Try Straight Away
        if (TrySetTarget(ctx, bestDir)) return;

        // B. Try Sideways (strafe)
        Vector3 sideDir = new Vector3(bestDir.z, 0, bestDir.x);
        if (TrySetTarget(ctx, sideDir)) return;
        if (TrySetTarget(ctx, -sideDir)) return;

        // C. Panic (Random valid neighbor)
        Vector3[] panicDirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        foreach (var d in panicDirs)
        {
            if (TrySetTarget(ctx, d)) return;
        }
    }

    private bool TrySetTarget(PawnContext ctx, Vector3 dir)
    {
        Vector3 targetColumn = ctx.transform.position + dir;

        // 1. Check Flat (Walk)
        if (Check(ctx, targetColumn)) return true;

        // 2. Check Up (Climb 1 block)
        if (Check(ctx, targetColumn + Vector3.up)) return true;

        // 3. Check Down (Jump off cliff)
        // We loop from 1 block down to 'maxJumpDrop'
        for (int i = 1; i <= maxJumpDrop; i++)
        {
            Vector3 dropTarget = targetColumn + (Vector3.down * i);

            // To safely jump, the block we land on must be walkable...
            if (VoxelPathHelper.IsWalkable(ctx.world, dropTarget))
            {
                // ...AND the air path to get there must be clear.
                // We check the blocks between our current height and the drop target.
                // If any of them are solid, we'd hit our head or get stuck inside.
                if (IsAirColumnClear(ctx.world, targetColumn, i))
                {
                    ctx.SetTargetAndSnap(dropTarget);
                    return true;
                }
            }
        }

        return false;
    }

    // Helper: Is it safe to move?
    private bool Check(PawnContext ctx, Vector3 target)
    {
        if (VoxelPathHelper.IsWalkable(ctx.world, target))
        {
            ctx.SetTargetAndSnap(target);
            return true;
        }
        return false;
    }

    // New Helper: Ensures we don't jump INSIDE a wall
    private bool IsAirColumnClear(VoxelWorld world, Vector3 columnPos, int dropHeight)
    {
        // Loop from the top (current level) down to the block just above the landing spot
        for (int i = 0; i < dropHeight; i++)
        {
            Vector3 airCheck = columnPos + (Vector3.down * i);

            // If any block in this vertical strip is NOT air, we can't fall through it.
            if (world.GetBlock(airCheck) != null)
                return false;
        }
        return true;
    }
}