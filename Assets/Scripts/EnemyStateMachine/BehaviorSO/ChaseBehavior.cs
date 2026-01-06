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
        if (prey == null) return;

        if (Vector3.Distance(ctx.transform.position, ctx.currentGridTarget) > 0.05f)
        {
            ctx.transform.position = Vector3.MoveTowards(
                ctx.transform.position,
                ctx.currentGridTarget,
                huntSpeed * Time.deltaTime
            );

            // Stabilized Look
            Vector3 moveDir = (ctx.currentGridTarget - ctx.transform.position);
            moveDir.y = 0;
            if (moveDir.sqrMagnitude > 0.05f)
            {
                Quaternion rot = Quaternion.LookRotation(moveDir);
                ctx.transform.rotation = Quaternion.Slerp(ctx.transform.rotation, rot, 10f * Time.deltaTime);
            }
            return;
        }

        // Logic
        Vector3 bestDir = VoxelPathHelper.GetCardinalDirection(ctx.transform.position, prey.position);

        if (TryMove(ctx, bestDir)) return;

        Vector3 altDir = new Vector3(bestDir.z, 0, bestDir.x);
        if (TryMove(ctx, altDir)) return;
        if (TryMove(ctx, -altDir)) return;
    }

    private bool TryMove(PawnContext ctx, Vector3 dir)
    {
        Vector3 target = ctx.transform.position + dir;

        // Try Flat
        if (VoxelPathHelper.IsWalkable(ctx.world, target))
        {
            ctx.SetTargetAndSnap(target);
            return true;
        }

        // Try Up
        if (VoxelPathHelper.IsWalkable(ctx.world, target + Vector3.up))
        {
            ctx.SetTargetAndSnap(target + Vector3.up);
            return true;
        }

        // Try Down
        if (VoxelPathHelper.IsWalkable(ctx.world, target + Vector3.down))
        {
            ctx.SetTargetAndSnap(target + Vector3.down);
            return true;
        }

        return false;
    }
}