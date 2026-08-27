using System.Collections.Generic;
using UnityEngine;

public static class VoxelPathHelper
{
    private const int MaxFallDistance = 32;

    private class PathNode
    {
        public Vector3Int pos;
        public PathNode parent;
        public int gCost;
        public int hCost;
        public int fCost => gCost + hCost;
    }

    public static bool IsWalkable(VoxelWorld world, Vector3 targetPos, bool checkPawns = true)
    {
        Vector3Int feetPos = new Vector3Int(Mathf.RoundToInt(targetPos.x), Mathf.RoundToInt(targetPos.y), Mathf.RoundToInt(targetPos.z));

        if (!IsTerrainWalkable(world, feetPos)) return false;

        // 2. PAWN CHECK
        if (checkPawns)
        {
            Vector3 checkCenter = feetPos + new Vector3(0.5f, 1.0f, 0.5f);
            Collider[] hits = Physics.OverlapBox(checkCenter, new Vector3(0.3f, 0.8f, 0.3f));
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<Pawn>() != null) return false;
            }

            foreach (var pawn in Pawn.ActivePawns)
            {
                if (pawn.LogicalGridPos == feetPos) return false;
            }
        }

        return true;
    }

    // --- A* PATHFINDING WITH PARTIAL PATH SUPPORT ---
    public static List<Vector3Int> FindPath(VoxelWorld world, Vector3Int start, Vector3Int target, int maxSearch = 5000, bool stopAdjacent = false, bool avoidSelf = false)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        HashSet<Vector3Int> occupiedPawns = GetPawnPositions();

        if (start == target) return path;
        if (stopAdjacent && IsAdjacent(start, target, avoidSelf)) return path;

        HashSet<Vector3Int> closed = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, PathNode> open = new Dictionary<Vector3Int, PathNode>();

        PathNode startNode = new PathNode { pos = start, gCost = 0, hCost = GetDist(start, target), parent = null };
        open.Add(start, startNode);

        // Keep track of the node closest to the target in case we run out of search iterations
        PathNode closestNode = startNode;
        int iterations = 0;

        while (open.Count > 0 && iterations < maxSearch)
        {
            iterations++;

            PathNode current = null;
            foreach (var node in open.Values)
            {
                if (current == null || node.fCost < current.fCost || (node.fCost == current.fCost && node.hCost < current.hCost))
                {
                    current = node;
                }
            }

            // Update the closest node we've seen so far
            if (current.hCost < closestNode.hCost)
            {
                closestNode = current;
            }

            // Check Win Condition (We made it all the way!)
            if ((!stopAdjacent && current.pos == target) || (stopAdjacent && IsAdjacent(current.pos, target, avoidSelf)))
            {
                return RetracePath(startNode, current);
            }

            open.Remove(current.pos);
            closed.Add(current.pos);

            // Get Neighbors
            Vector3Int[] dirs = { Vector3Int.forward, Vector3Int.back, Vector3Int.left, Vector3Int.right };
            bool canStepUp = world.GetBlock(current.pos + (Vector3Int.up * 2)) == null; // Head clearance to jump

            foreach (var d in dirs)
            {
                Vector3Int basePos = current.pos + d;
                Vector3Int actualPos = basePos;
                bool valid = false;

                if (canStepUp && IsWalkableForPath(world, basePos + Vector3Int.up, occupiedPawns)) { actualPos = basePos + Vector3Int.up; valid = true; }
                else if (IsWalkableForPath(world, basePos, occupiedPawns)) { actualPos = basePos; valid = true; }
                else if (IsWalkableForPath(world, basePos + Vector3Int.down, occupiedPawns)) { actualPos = basePos + Vector3Int.down; valid = true; }

                if (!valid && TryFindFallLanding(world, basePos, occupiedPawns, out Vector3Int fallLanding))
                {
                    actualPos = fallLanding;
                    valid = true;
                }

                if (!valid || closed.Contains(actualPos)) continue;

                int moveCost = current.gCost + 10;
                if (actualPos.y > current.pos.y)
                {
                    moveCost += 5;
                }
                else if (actualPos.y < current.pos.y)
                {
                    int fallDistance = current.pos.y - actualPos.y;
                    moveCost += 5 + (fallDistance * fallDistance * 10);
                }

                if (!open.TryGetValue(actualPos, out PathNode neighborNode))
                {
                    neighborNode = new PathNode { pos = actualPos, parent = current, gCost = moveCost, hCost = GetDist(actualPos, target) };
                    open.Add(actualPos, neighborNode);
                }
                else if (moveCost < neighborNode.gCost)
                {
                    neighborNode.gCost = moveCost;
                    neighborNode.parent = current;
                }
            }
        }

        // OUT OF RESOURCES OR NO PATH. 
        // If we found a node closer than where we started, return a partial path to that node.
        if (closestNode != startNode)
        {
            return RetracePath(startNode, closestNode);
        }

        return null; // Totally trapped, can't move anywhere closer.
    }

    private static List<Vector3Int> RetracePath(PathNode startNode, PathNode endNode)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        PathNode current = endNode;

        while (current != startNode)
        {
            path.Add(current.pos);
            current = current.parent;
        }
        path.Reverse();
        return path;
    }

    private static bool IsTerrainWalkable(VoxelWorld world, Vector3Int feetPos)
    {
        if (world.GetBlock(feetPos + Vector3Int.down) == null) return false;
        if (world.GetBlock(feetPos) != null) return false;
        if (world.GetBlock(feetPos + Vector3Int.up) != null) return false;
        return true;
    }

    private static bool IsWalkableForPath(VoxelWorld world, Vector3Int feetPos, HashSet<Vector3Int> occupiedPawns)
    {
        return IsTerrainWalkable(world, feetPos) && !occupiedPawns.Contains(feetPos);
    }

    private static bool TryFindFallLanding(VoxelWorld world, Vector3Int basePos, HashSet<Vector3Int> occupiedPawns, out Vector3Int landing)
    {
        landing = basePos;

        if (world.GetBlock(basePos) != null) return false;

        for (int distance = 2; distance <= MaxFallDistance; distance++)
        {
            Vector3Int candidate = basePos + (Vector3Int.down * distance);

            if (world.GetBlock(candidate) != null || world.GetBlock(candidate + Vector3Int.up) != null)
                break;

            if (IsWalkableForPath(world, candidate, occupiedPawns))
            {
                landing = candidate;
                return true;
            }
        }

        return false;
    }

    private static HashSet<Vector3Int> GetPawnPositions()
    {
        HashSet<Vector3Int> positions = new HashSet<Vector3Int>();
        foreach (var pawn in Pawn.ActivePawns)
        {
            positions.Add(pawn.LogicalGridPos);
        }
        return positions;
    }

    private static int GetDist(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z);
    }

    public static bool IsAdjacent(Vector3Int pawnPos, Vector3Int targetPos, bool avoidSelf = false)
    {
        if (avoidSelf)
        {
            if (pawnPos == targetPos || pawnPos + Vector3Int.up == targetPos) return false;
        }
        else
        {
            if (pawnPos == targetPos) return false;
        }

        return Mathf.Abs(pawnPos.x - targetPos.x) <= 1 &&
               Mathf.Abs(pawnPos.y - targetPos.y) <= 2 &&
               Mathf.Abs(pawnPos.z - targetPos.z) <= 1;
    }
}