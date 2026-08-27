using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PawnJobTracker
{
    private static readonly HashSet<PawnJobTracker> activeTrackers = new HashSet<PawnJobTracker>();

    private Pawn pawn;
    private Job currentJob;
    private JobDriver currentDriver;

    private List<Job> pendingJobs = new List<Job>();

    public PawnJobTracker(Pawn pawn)
    {
        this.pawn = pawn;
        activeTrackers.Add(this);
    }

    public void Dispose()
    {
        activeTrackers.Remove(this);
    }

    public static bool IsBuildOrderReady(Job job)
    {
        if (job.Def != JobDef.Place || job.BuildPlanId == 0) return true;

        foreach (var tracker in activeTrackers)
        {
            if (tracker.HasEarlierBuildJob(job)) return false;
        }

        return true;
    }

    private bool HasEarlierBuildJob(Job job)
    {
        if (currentJob != null && currentJob.Def == JobDef.Place &&
            currentJob.BuildPlanId == job.BuildPlanId &&
            currentJob.BuildOrder < job.BuildOrder &&
            (currentDriver == null || !currentDriver.IsFinished))
        {
            return true;
        }

        foreach (var pendingJob in pendingJobs)
        {
            if (pendingJob.Def == JobDef.Place &&
                pendingJob.BuildPlanId == job.BuildPlanId &&
                pendingJob.BuildOrder < job.BuildOrder)
            {
                return true;
            }
        }

        return false;
    }

    public void QueueJob(Job job)
    {
        pendingJobs.Add(job);
    }

    public void ClearJobs()
    {
        pendingJobs.Clear();
        currentJob = null;
        currentDriver = null;
    }

    public void ExecuteTracker(float simDeltaTime)
    {
        if (currentJob == null || currentDriver == null || currentDriver.IsFinished)
        {
            StartNextJob();
        }

        if (currentDriver != null)
        {
            currentDriver.Execute(simDeltaTime);
        }
    }

    private void StartNextJob()
    {
        currentJob = GetBestJob();

        if (currentJob == null)
        {
            currentJob = new Job(JobDef.Wander);
        }
        else
        {
            pendingJobs.Remove(currentJob);
        }

        currentDriver = CreateDriver(currentJob.Def);
        currentDriver.Bind(pawn, currentJob);
    }

    private Job GetBestJob()
    {
        if (pendingJobs.Count == 0) return null;

        // Clean up unreachable jobs so they don't permanently clog the system
        pendingJobs.RemoveAll(j => j.FailCount > 10);
        if (pendingJobs.Count == 0) return null;

        Vector3 pawnPos = pawn.transform.position;

        // 1. Prioritize MINING (Clear the area before building)
        var mineJobs = pendingJobs.Where(j => j.Def == JobDef.Mine).ToList();
        if (mineJobs.Count > 0)
        {
            mineJobs.Sort((a, b) =>
            {
                // A. Strict Top-Down (ALWAYS mine the highest blocks first to prevent floating blocks/digging the floor out)
                if (a.TargetGrid.y != b.TargetGrid.y)
                    return b.TargetGrid.y.CompareTo(a.TargetGrid.y);

                // B. Try fresh jobs before retrying failed ones (Only compares blocks on the SAME layer)
                if (a.FailCount != b.FailCount)
                    return a.FailCount.CompareTo(b.FailCount);

                // C. Closest to Pawn
                float distA = Vector3.SqrMagnitude((Vector3)a.TargetGrid - pawnPos);
                float distB = Vector3.SqrMagnitude((Vector3)b.TargetGrid - pawnPos);
                return distA.CompareTo(distB);
            });
            return mineJobs[0];
        }

        // 2. Proceed to PLACING
        var placeJobs = pendingJobs.Where(j => j.Def == JobDef.Place).ToList();
        if (placeJobs.Count > 0)
        {
            placeJobs.Sort((a, b) =>
            {
                // A. Strict Bottom-Up (ALWAYS finish lower layers before moving up. Prevents trapping gaps)
                if (a.TargetGrid.y != b.TargetGrid.y)
                    return a.TargetGrid.y.CompareTo(b.TargetGrid.y);

                // B. Try fresh jobs before retrying failed ones (Only compares blocks on the SAME layer)
                if (a.FailCount != b.FailCount)
                    return a.FailCount.CompareTo(b.FailCount);

                // C. Preserve the planned construction sequence
                return a.BuildOrder.CompareTo(b.BuildOrder);
            });
            return placeJobs[0];
        }

        // 3. Fallback for Move/Wander or custom jobs (First-In-First-Out)
        return pendingJobs[0];
    }

    private JobDriver CreateDriver(JobDef def)
    {
        switch (def)
        {
            case JobDef.Wander: return new JobDriver_Wander();
            case JobDef.Move: return new JobDriver_Move();
            case JobDef.Mine: return new JobDriver_Mine();
            case JobDef.Place: return new JobDriver_Place();
            default: return new JobDriver_Wander();
        }
    }
}