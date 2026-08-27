using UnityEngine;

public class JobDriver_Place : JobDriver
{
    private float placeTimer = 0f;

    public override void Execute(float simDeltaTime)
    {
        if (!PawnJobTracker.IsBuildOrderReady(job)) return;

        BlockData blockToPlace = job.BlockToPlace;
        if (blockToPlace != null && (blockToPlace.maxToolUses > 0 || !pawn.HasItem(blockToPlace)))
        {
            blockToPlace = null;
        }

        foreach (var slot in pawn.inventory)
        {
            if (blockToPlace == null && !slot.IsEmpty && slot.block.maxToolUses <= 0)
            {
                blockToPlace = slot.block;
                break;
            }
        }

        if (blockToPlace == null)
        {
            IsFinished = true;
            return;
        }

        BlockData existingBlock = pawn.World.GetBlock(job.TargetGrid);
        if (existingBlock != null)
        {
            IsFinished = true;
            return;
        }

        if (!VoxelPathHelper.IsAdjacent(pawn.LogicalGridPos, job.TargetGrid, avoidSelf: true))
        {
            if (pawn.IsMoving) return;

            if (currentPath == null)
            {
                currentPath = VoxelPathHelper.FindPath(pawn.World, pawn.LogicalGridPos, job.TargetGrid, 5000, stopAdjacent: true, avoidSelf: true);
                pathIndex = 0;

                // If it returns empty/null, we walked as close as we could but are blocked by terrain
                if (currentPath == null || currentPath.Count == 0)
                {
                    job.FailCount++;
                    if (job.FailCount < 10) pawn.JobTracker.QueueJob(job);
                    IsFinished = true;
                    return;
                }
            }

            if (pathIndex < currentPath.Count)
            {
                Vector3Int nextStep = currentPath[pathIndex];
                if (VoxelPathHelper.IsWalkable(pawn.World, nextStep))
                {
                    pawn.StartMovingTo(nextStep);
                    pathIndex++;
                }
                else
                {
                    currentPath = null;
                }
            }
            else
            {
                // Reached the end of the partial path but still not adjacent. Recalculate!
                currentPath = null;
            }
            return;
        }

        placeTimer += simDeltaTime;

        if (placeTimer >= 0.2f)
        {
            Collider[] hits = Physics.OverlapBox((Vector3)job.TargetGrid + new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.3f, 0.3f, 0.3f));
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<Pawn>() != null)
                {
                    placeTimer = 0f;
                    return;
                }
            }

            pawn.ConsumeItem(blockToPlace);
            pawn.World.ModifyBlock(job.TargetGrid, blockToPlace);
            IsFinished = true;
        }
    }
}