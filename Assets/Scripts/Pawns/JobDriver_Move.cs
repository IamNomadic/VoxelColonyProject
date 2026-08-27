using UnityEngine;

public class JobDriver_Move : JobDriver
{
    public override void Execute(float simDeltaTime)
    {
        if (pawn.LogicalGridPos == job.TargetGrid)
        {
            IsFinished = true;
            return;
        }

        if (pawn.IsMoving) return;

        if (currentPath == null)
        {
            currentPath = VoxelPathHelper.FindPath(pawn.World, pawn.LogicalGridPos, job.TargetGrid, 5000, false);
            pathIndex = 0;

            // If the path is empty, we are physically as close as we can get but still blocked
            if (currentPath == null || currentPath.Count == 0)
            {
                Debug.Log($"{pawn.name}: Path to target is blocked or too far. Ending move job early.");
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
            // We finished the partial path, but we still aren't at the target!
            // Clear the path so we run a fresh A* check from our new location on the next frame.
            currentPath = null;
        }
    }
}