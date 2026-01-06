using UnityEngine;

public static class VoxelPathHelper
{
    public static bool IsWalkable(VoxelWorld world, Vector3 targetGridPos, bool checkPawns = true)
    {
        // 1. DATA CHECK: Is there terrain here?
        // Head must be empty (Air)
        if (world.GetBlock(targetGridPos) != null) return false;

        // Feet must be solid (Ground)
        if (world.GetBlock(targetGridPos + Vector3.down) == null) return false;

        // 2. PAWN CHECK: Is another pawn standing here?
        if (checkPawns)
        {
            // We use OverlapSphere so we can filter OUT the ground mesh.
            // We only care if we hit another Pawn.
            Collider[] hits = Physics.OverlapSphere(targetGridPos, 0.3f);
            foreach (var hit in hits)
            {
                // If we hit something that has an Identity script, it's a pawn. Block movement.
                if (hit.GetComponent<PawnIdentity>() != null)
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