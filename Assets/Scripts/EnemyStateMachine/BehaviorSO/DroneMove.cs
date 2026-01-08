using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Behaviors/Drone Move Queue")]
public class State_DroneMove : PawnStateBehaviour
{
    public float moveSpeed = 4f;
    public float stopDistance = 0.2f;
    public float gravitySpeed = 10f; // How fast we snap down slopes

    public override void Enter(PawnContext ctx)
    {
        // If we entered this state but haven't picked a target yet, pick one immediately
        if (ctx.currentCommandTarget == null && ctx.commandQueue.Count > 0)
        {
            ctx.currentCommandTarget = ctx.commandQueue.Dequeue();
        }
    }

    public override void Execute(PawnContext ctx)
    {
        // 1. Queue Management
        if (ctx.currentCommandTarget == null)
        {
            if (ctx.commandQueue.Count > 0) ctx.currentCommandTarget = ctx.commandQueue.Dequeue();
            else return; // Transition will handle exit
        }

        Vector3 target = ctx.currentCommandTarget.Value;
        Vector3 currentPos = ctx.transform.position;

        // 2. Check Arrival (Ignore Y for forgiving arrival)
        Vector3 targetFlat = new Vector3(target.x, currentPos.y, target.z);
        if (Vector3.Distance(currentPos, targetFlat) <= stopDistance)
        {
            ctx.currentCommandTarget = null; // Arrived. Next!
            return;
        }

        // 3. Horizontal Movement
        Vector3 dir = (targetFlat - currentPos).normalized;
        Vector3 nextPos = currentPos + (dir * moveSpeed * Time.deltaTime);

        // 4. TERRAIN SNAPPING (The Fix)
        // We handle gravity manually here because the main StateMachine turns off gravity when "Landed"

        // A. Check for wall (Step Up)
        BlockData blockAtFeet = ctx.world.GetBlock(nextPos);
        if (blockAtFeet != null)
        {
            // There is a block in front of our feet. Try to step up.
            nextPos.y += 1.0f * moveSpeed * Time.deltaTime;
        }
        else
        {
            // B. Check for cliff (Step Down)
            // Look 1 block below the NEW position
            BlockData blockBelow = ctx.world.GetBlock(nextPos + Vector3.down * 0.5f);

            // If there is AIR below us, apply gravity
            if (blockBelow == null)
            {
                nextPos.y -= gravitySpeed * Time.deltaTime;
            }
            else
            {
                // Optional: Snap perfectly to block surface to avoid jitter
                // nextPos.y = Mathf.Floor(nextPos.y) + ctx.data.verticalOffset;
            }
        }

        // 5. Apply Move
        ctx.transform.position = nextPos;

        // 6. Rotation
        if (dir != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            ctx.transform.rotation = Quaternion.Slerp(ctx.transform.rotation, lookRot, 10f * Time.deltaTime);
        }
    }

    public override void Exit(PawnContext ctx)
    {
        // Cleanup if needed
    }
}