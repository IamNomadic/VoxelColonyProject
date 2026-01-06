using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Behaviours/Voxel Flee")]
public class VoxelFleeState : PawnStateBehaviour
{
    public float panicSpeed = 7.0f;
    public PawnType dangerType = PawnType.Predator;

    public override void Enter(PawnContext ctx) { }
    public override void Exit(PawnContext ctx) { }

    public override void Execute(PawnContext ctx)
    {
        Transform threat = ctx.ScanForTarget(dangerType);
        if (threat == null) return;

        if (Vector3.Distance(ctx.transform.position, ctx.currentGridTarget) > 0.05f)
        {
            ctx.transform.position = Vector3.MoveTowards(
                ctx.transform.position,
                ctx.currentGridTarget,
                panicSpeed * Time.deltaTime
            );

            // Stabilized Look
            Vector3 moveDir = (ctx.currentGridTarget - ctx.transform.position);
            moveDir.y = 0;
            if (moveDir.sqrMagnitude > 0.05f)
            {
                Quaternion rot = Quaternion.LookRotation(moveDir);
                ctx.transform.rotation = Quaternion.Slerp(ctx.transform.rotation, rot, 15f * Time.deltaTime);
            }
            return;
        }

        Vector3 dirToThreat = threat.position - ctx.transform.position;
        Vector3 dirAway = -dirToThreat;

        Vector3 bestDir = Vector3.zero;
        if (Mathf.Abs(dirAway.x) > Mathf.Abs(dirAway.z))
            bestDir = new Vector3(Mathf.Sign(dirAway.x), 0, 0);
        else
            bestDir = new Vector3(0, 0, Mathf.Sign(dirAway.z));

        if (TrySetTarget(ctx, bestDir)) return;

        Vector3 sideDir = new Vector3(bestDir.z, 0, bestDir.x);
        if (TrySetTarget(ctx, sideDir)) return;
        if (TrySetTarget(ctx, -sideDir)) return;

        Vector3[] panicDirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        foreach (var d in panicDirs)
        {
            if (TrySetTarget(ctx, d)) return;
        }
    }

    private bool TrySetTarget(PawnContext ctx, Vector3 dir)
    {
        Vector3 flat = ctx.transform.position + dir;

        if (VoxelPathHelper.IsWalkable(ctx.world, flat))
        {
            ctx.SetTargetAndSnap(flat);
            return true;
        }
        if (VoxelPathHelper.IsWalkable(ctx.world, flat + Vector3.up))
        {
            ctx.SetTargetAndSnap(flat + Vector3.up);
            return true;
        }
        if (VoxelPathHelper.IsWalkable(ctx.world, flat + Vector3.down))
        {
            ctx.SetTargetAndSnap(flat + Vector3.down);
            return true;
        }
        return false;
    }
}