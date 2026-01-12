using System.Collections.Generic;
using UnityEngine;

// Lightweight occupancy map for integer voxel grid positions
public class VoxelOccupancy
{
    // store occupied grid positions
    HashSet<Vector3Int> occupied = new HashSet<Vector3Int>();

    // spacing used by your VoxelCreator (must match)
    public float spacing = 1f;

    public VoxelOccupancy(float spacing)
    {
        this.spacing = spacing;
    }

    // Build occupancy from world voxel positions (spawnedVoxels)
    // pass worldPositions: each position is the world-space center of the voxel
    public void BuildFromPositions(IEnumerable<Vector3> worldPositions)
    {
        occupied.Clear();
        foreach (var wp in worldPositions)
        {
            Vector3Int idx = WorldToGrid(wp);
            occupied.Add(idx);
        }
    }

    // Add/Remove single voxel (for incremental updates)
    public void AddAtWorldPos(Vector3 worldPos) => occupied.Add(WorldToGrid(worldPos));
    public void RemoveAtWorldPos(Vector3 worldPos) => occupied.Remove(WorldToGrid(worldPos));

    // Convert world position to integer grid coordinate
    public Vector3Int WorldToGrid(Vector3 worldPos)
    {
        // Map world position to integer cell using floor so a center at e.g. 1.0 maps into cell 1 if spacing==1.
        int x = Mathf.FloorToInt(worldPos.x / spacing);
        int y = Mathf.FloorToInt(worldPos.y / spacing);
        int z = Mathf.FloorToInt(worldPos.z / spacing);
        return new Vector3Int(x, y, z);
    }

    // Convert grid coordinate back to world center
    public Vector3 GridToWorld(Vector3Int g) => new Vector3(g.x * spacing, g.y * spacing, g.z * spacing);

    // Check occupancy
    public bool IsOccupied(Vector3Int g) => occupied.Contains(g);

    // 3D DDA: returns true if line from originWorld to targetWorld is clear of occupied voxels
    // excludeTarget true means the target voxel itself doesn't count as a blocker
    public bool LineOfSightClear(Vector3 originWorld, Vector3 targetWorld, bool excludeTarget = true)
    {
        Vector3Int start = WorldToGrid(originWorld);
        Vector3Int end = WorldToGrid(targetWorld);

        // If origin inside a voxel, treat as visible
        if (start == end) return true;

        // 3D DDA implementation (Amanatides and Woo)
        Vector3 startF = new Vector3(originWorld.x, originWorld.y, originWorld.z);
        Vector3 endF = new Vector3(targetWorld.x, targetWorld.y, targetWorld.z);
        Vector3 direction = (endF - startF);
        float length = direction.magnitude;
        if (length <= 1e-6f) return true;
        Vector3 dir = direction / length;

        int x = start.x, y = start.y, z = start.z;
        int stepX = dir.x > 0 ? 1 : (dir.x < 0 ? -1 : 0);
        int stepY = dir.y > 0 ? 1 : (dir.y < 0 ? -1 : 0);
        int stepZ = dir.z > 0 ? 1 : (dir.z < 0 ? -1 : 0);

        // compute distance to first voxel boundary using standard Amanatides & Woo cell maths
        float voxelMinX = x * spacing;
        float voxelMinY = y * spacing;
        float voxelMinZ = z * spacing;

        float nextBoundaryX = (stepX > 0) ? (voxelMinX + spacing) : voxelMinX;
        float nextBoundaryY = (stepY > 0) ? (voxelMinY + spacing) : voxelMinY;
        float nextBoundaryZ = (stepZ > 0) ? (voxelMinZ + spacing) : voxelMinZ;

        // if dir component is zero, tMax = +inf; otherwise compute
        float tMaxX = (stepX == 0) ? float.PositiveInfinity : (nextBoundaryX - startF.x) / dir.x;
        float tMaxY = (stepY == 0) ? float.PositiveInfinity : (nextBoundaryY - startF.y) / dir.y;
        float tMaxZ = (stepZ == 0) ? float.PositiveInfinity : (nextBoundaryZ - startF.z) / dir.z;

        float tDeltaX = (stepX == 0) ? float.PositiveInfinity : (spacing / Mathf.Abs(dir.x));
        float tDeltaY = (stepY == 0) ? float.PositiveInfinity : (spacing / Mathf.Abs(dir.y));
        float tDeltaZ = (stepZ == 0) ? float.PositiveInfinity : (spacing / Mathf.Abs(dir.z));


        // traverse until we pass the end point
        float t = 0f;
        float tEnd = length;

        // first voxel might be occupied but if origin is inside it we allowed above. start stepping
        while (t <= tEnd)
        {
            Vector3Int current = new Vector3Int(x, y, z);

            // if current is occupied and not the target (or excludeTarget false), it's blocked
            if (occupied.Contains(current))
            {
                // If this is the target voxel and excludeTarget is true, treat as visible
                if (!(excludeTarget && current == end))
                    return false;
            }

            // step to next voxel
            if (tMaxX < tMaxY)
            {
                if (tMaxX < tMaxZ)
                {
                    x += stepX;
                    t = tMaxX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    z += stepZ;
                    t = tMaxZ;
                    tMaxZ += tDeltaZ;
                }
            }
            else
            {
                if (tMaxY < tMaxZ)
                {
                    y += stepY;
                    t = tMaxY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    z += stepZ;
                    t = tMaxZ;
                    tMaxZ += tDeltaZ;
                }
            }
        }

        return true;
    }
}
