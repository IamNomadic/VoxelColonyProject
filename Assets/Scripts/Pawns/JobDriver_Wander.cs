using UnityEngine;

public class JobDriver_Wander : JobDriver
{
    private enum WanderState { Waiting, Moving }

    private WanderState state;
    private float waitTimer;

    public override void Bind(Pawn pawn, Job job)
    {
        base.Bind(pawn, job);
        state = WanderState.Waiting;
        waitTimer = Random.Range(1.0f, 3.0f);
    }

    public override void Execute(float simDeltaTime)
    {
        if (state == WanderState.Waiting)
        {
            waitTimer -= simDeltaTime;
            if (waitTimer <= 0f)
            {
                Vector3Int nextPos = GetRandomWalkableAdjacent();
                if (nextPos != pawn.LogicalGridPos)
                {
                    pawn.StartMovingTo(nextPos);
                    state = WanderState.Moving;
                }
                else
                {
                    IsFinished = true;
                }
            }
        }
        else if (state == WanderState.Moving)
        {
            if (!pawn.IsMoving)
            {
                IsFinished = true;
            }
        }
    }

    private Vector3Int GetRandomWalkableAdjacent()
    {
        Vector3Int[] dirs = { Vector3Int.forward, Vector3Int.back, Vector3Int.left, Vector3Int.right };
        Vector3Int bestPos = pawn.LogicalGridPos;

        // CLEARANCE CHECK: Requires head clearance to jump up
        bool canStepUp = pawn.World.GetBlock(bestPos + (Vector3Int.up * 2)) == null;

        for (int i = 0; i < dirs.Length; i++)
        {
            Vector3Int temp = dirs[i];
            int r = Random.Range(i, dirs.Length);
            dirs[i] = dirs[r];
            dirs[r] = temp;
        }

        foreach (var dir in dirs)
        {
            Vector3Int basePos = pawn.LogicalGridPos + dir;

            if (canStepUp && VoxelPathHelper.IsWalkable(pawn.World, basePos + Vector3Int.up))
                return basePos + Vector3Int.up;

            if (VoxelPathHelper.IsWalkable(pawn.World, basePos))
                return basePos;

            if (VoxelPathHelper.IsWalkable(pawn.World, basePos + Vector3Int.down))
                return basePos + Vector3Int.down;

            for (int fallDistance = 2; fallDistance <= 32; fallDistance++)
            {
                Vector3Int fallPos = basePos + (Vector3Int.down * fallDistance);
                if (pawn.World.GetBlock(fallPos) != null || pawn.World.GetBlock(fallPos + Vector3Int.up) != null)
                    break;

                if (VoxelPathHelper.IsWalkable(pawn.World, fallPos))
                    return fallPos;
            }
        }

        return bestPos;
    }
}