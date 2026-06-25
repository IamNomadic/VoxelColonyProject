using UnityEngine;

public static class VoxelPathHelper
{
    public static bool IsWalkable(VoxelWorld world, Vector3 targetGridPos, bool checkPawns = true)
    {
        // 1. DATA CHECK
        if (world.GetBlock(targetGridPos) != null) return false;
        if (world.GetBlock(targetGridPos + Vector3.down) == null) return false;

        // 2. PAWN CHECK
        if (checkPawns)
        {
            Collider[] hits = Physics.OverlapSphere(targetGridPos, 0.3f);
            foreach (var hit in hits)
            {
                // FIX: Check for StateMachine, not Identity
                if (hit.GetComponent<PawnStateMachine>() != null)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static Vector3 GetCardinalDirection(Vector3 origin, Vector3 target)
    {
        Vector3 dir = (target - origin).normalized;
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z))
            return new Vector3(Mathf.Sign(dir.x), 0, 0);
        else
            return new Vector3(0, 0, Mathf.Sign(dir.z));
    }
}