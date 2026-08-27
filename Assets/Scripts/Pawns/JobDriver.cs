using System.Collections.Generic;
using UnityEngine;

public abstract class JobDriver
{
    protected Pawn pawn;
    protected Job job;
    public bool IsFinished { get; protected set; }

    // Pathfinding Storage
    protected List<Vector3Int> currentPath;
    protected int pathIndex;

    public virtual void Bind(Pawn pawn, Job job)
    {
        this.pawn = pawn;
        this.job = job;
        this.IsFinished = false;

        this.currentPath = null;
        this.pathIndex = 0;
    }

    public abstract void Execute(float simDeltaTime);
}