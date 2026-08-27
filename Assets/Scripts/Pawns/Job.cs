using UnityEngine;

public enum JobDef
{
    None,
    Wander,
    Move,
    Mine,
    Place
}

public class Job
{
    public JobDef Def;
    public Vector3Int TargetGrid;
    public BlockData BlockToPlace; // Used exclusively for Place jobs
    public int FailCount = 0;      // Tracks how many times the job was unreachable
    public int BuildOrder = 0;
    public int BuildPlanId = 0;

    public Job(JobDef def, Vector3Int targetGrid = default, BlockData blockToPlace = null, int buildOrder = 0, int buildPlanId = 0)
    {
        Def = def;
        TargetGrid = targetGrid;
        BlockToPlace = blockToPlace;
        FailCount = 0;
        BuildOrder = buildOrder;
        BuildPlanId = buildPlanId;
    }
}