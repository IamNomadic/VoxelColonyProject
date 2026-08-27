using UnityEngine;

public class JobDriver_Mine : JobDriver
{
    private float miningProgress = 0f;

    public override void Execute(float simDeltaTime)
    {
        BlockData targetBlock = pawn.World.GetBlock(job.TargetGrid);
        if (targetBlock == null)
        {
            IsFinished = true;
            return;
        }

        if (!VoxelPathHelper.IsAdjacent(pawn.LogicalGridPos, job.TargetGrid))
        {
            if (pawn.IsMoving) return;

            if (currentPath == null)
            {
                currentPath = VoxelPathHelper.FindPath(pawn.World, pawn.LogicalGridPos, job.TargetGrid, 5000, true);
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

        float totalMiningSpeed = 1f;
        InventorySlot bestTool = null;

        foreach (var slot in pawn.inventory)
        {
            if (!slot.IsEmpty && slot.block.toolType != ToolType.None)
            {
                if (slot.block.toolType == targetBlock.preferredTool)
                {
                    if (slot.block.toolSpeedMultiplier > totalMiningSpeed)
                    {
                        totalMiningSpeed = slot.block.toolSpeedMultiplier;
                        bestTool = slot;
                    }
                }
            }
        }

        miningProgress += simDeltaTime * totalMiningSpeed;

        if (miningProgress >= targetBlock.durability)
        {
            Vector3 spawnPos = new Vector3(job.TargetGrid.x + 0.5f, job.TargetGrid.y + 0.5f, job.TargetGrid.z + 0.5f);
            GameObject dropObj = new GameObject("Drop_" + targetBlock.blockName);
            VoxelItemDrop dropScript = dropObj.AddComponent<VoxelItemDrop>();
            dropScript.Initialize(targetBlock, spawnPos, 1, -1);

            pawn.World.ModifyBlock(job.TargetGrid, null);

            if (bestTool != null && bestTool.block.maxToolUses > 0)
            {
                bestTool.durability--;
                if (bestTool.durability <= 0) bestTool.Clear();
            }

            IsFinished = true;
        }
    }
}