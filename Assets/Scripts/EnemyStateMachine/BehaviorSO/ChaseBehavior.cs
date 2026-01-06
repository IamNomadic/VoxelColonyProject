using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Behaviours/Voxel Chase")]
public class VoxelChaseState : PawnStateBehaviour
{
    public float huntSpeed = 5.0f;
    public PawnType preyType = PawnType.Prey;

    public override void Enter(PawnContext ctx) { }
    public override void Exit(PawnContext ctx) { }

    public override void Execute(PawnContext ctx)
    {
        Transform prey = ctx.ScanForTarget(preyType);

        // If no prey visible, just wait here (Transitions will handle switching states)
        if (prey == null) return;

        // 1. If currently moving between blocks, finish that move first
        if (Vector3.Distance(ctx.transform.position, ctx.currentGridTarget) > 0.05f)
        {
            ctx.transform.position = Vector3.MoveTowards(
                ctx.transform.position,
                ctx.currentGridTarget,
                huntSpeed * Time.deltaTime
            );

            // Look at prey
            Vector3 lookPos = prey.position;
            lookPos.y = ctx.transform.position.y;
            ctx.transform.LookAt(lookPos);
            return;
        }

        // 2. We are stopped at a grid center. Calculate NEXT move.
        // Get generic direction towards prey (N/S/E/W)
        Vector3 bestDir = VoxelPathHelper.GetCardinalDirection(ctx.transform.position, prey.position);

        // Try the direct path
        if (TryMove(ctx, bestDir)) return;

        // If direct path blocked, try perpendicular (flanking)
        Vector3 altDir = new Vector3(bestDir.z, 0, bestDir.x);
        if (TryMove(ctx, altDir)) return;

        // Try other perpendicular
        if (TryMove(ctx, -altDir)) return;
    }

    private bool TryMove(PawnContext ctx, Vector3 dir)
    {
        // Try Flat
        Vector3 target = ctx.transform.position + dir;
        if (VoxelPathHelper.IsWalkable(ctx.world, target)) { ctx.currentGridTarget = target; return true; }

        // Try Up
        if (VoxelPathHelper.IsWalkable(ctx.world, target + Vector3.up)) { ctx.currentGridTarget = target + Vector3.up; return true; }

        // Try Down
        if (VoxelPathHelper.IsWalkable(ctx.world, target + Vector3.down)) { ctx.currentGridTarget = target + Vector3.down; return true; }

        return false;
    }
}