using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Behaviors/Drone Move Queue")]
public class State_DroneMove : PawnStateBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 4f;
    public float rotationSpeed = 10f;

    [Header("Physics Settings")]
    [Tooltip("Add 1.0 if pivot is at center. Add 0 if pivot is at feet.")]
    public float pivotOffset = 1.0f;
    public float maxStepHeight = 1.1f;
    public float gravitySpeed = 15f;

    public override void Enter(PawnContext ctx)
    {
        if (ctx.currentOrder == null && ctx.orderQueue.Count > 0)
        {
            ctx.currentOrder = ctx.orderQueue.Dequeue();
        }
    }

    public override void Execute(PawnContext ctx)
    {
        // 1. Manage Queue
        if (ctx.currentOrder == null)
        {
            if (ctx.orderQueue.Count > 0) ctx.currentOrder = ctx.orderQueue.Dequeue();
            else return;
        }

        // 2. Validate Target (Skip if block broken)
        Vector3 target = ctx.currentOrder.Value.target;
        if (ctx.currentOrder.Value.type == OrderType.Break)
        {
            if (ctx.world.GetBlock(target) == null)
            {
                ctx.currentOrder = null;
                return;
            }
        }

        // 3. Check Arrival
        Vector3 currentPos = ctx.transform.position;
        Vector3 targetFlat = new Vector3(target.x, currentPos.y, target.z);
        float dist = Vector3.Distance(currentPos, targetFlat);
        float stopDist = (ctx.currentOrder.Value.type == OrderType.Break) ? 1.2f : 0.2f;

        if (dist <= stopDist)
        {
            if (ctx.currentOrder.Value.type == OrderType.Move) ctx.currentOrder = null;
            return;
        }

        // 4. CALCULATE MOVEMENT
        Vector3 moveDir = (targetFlat - currentPos).normalized;
        Vector3 intendedPos = currentPos + (moveDir * moveSpeed * Time.deltaTime);

        // 5. COLLISION & TERRAIN LOGIC
        // A. Get Raw Floor Height (Top of the block)
        float nextFloorY = GetGroundHeight(ctx, intendedPos);

        // B. Apply Pivot Offset (The Fix: Lift the body up so feet sit on floor)
        float targetBodyY = nextFloorY + pivotOffset;

        // C. Calculate Step Height relative to our current FEET (not waist)
        float currentFeetY = currentPos.y - pivotOffset;
        float heightDiff = nextFloorY - currentFeetY;

        Vector3 finalPos = currentPos;

        // CASE A: Wall / Too High (Stop)
        if (heightDiff > maxStepHeight)
        {
            // Falling logic only (ignore X/Z movement)
            // If we are high in the air, fall. If on ground, stay put.
            if (currentPos.y > targetBodyY)
            {
                finalPos.y = Mathf.MoveTowards(currentPos.y, targetBodyY, gravitySpeed * Time.deltaTime);
            }
        }
        // CASE B: Walkable
        else
        {
            finalPos = intendedPos;

            // Snap / Gravity Logic
            if (currentPos.y > targetBodyY + 0.5f)
            {
                // Freefall
                finalPos.y = Mathf.MoveTowards(currentPos.y, targetBodyY, gravitySpeed * Time.deltaTime);
            }
            else
            {
                // Smooth Snap (Walking up stairs/slopes)
                finalPos.y = Mathf.Lerp(currentPos.y, targetBodyY, 20f * Time.deltaTime);
            }
        }

        // 6. APPLY
        ctx.transform.position = finalPos;

        if (moveDir != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(moveDir);
            ctx.transform.rotation = Quaternion.Slerp(ctx.transform.rotation, lookRot, rotationSpeed * Time.deltaTime);
        }
    }

    private float GetGroundHeight(PawnContext ctx, Vector3 pos)
    {
        // 1. Check Feet (Inside Block?)
        BlockData feetBlock = ctx.world.GetBlock(pos + Vector3.up * 0.1f);
        if (feetBlock != null) return Mathf.Floor(pos.y) + 1.0f;

        // 2. Check Below (Ground?)
        int checkX = Mathf.FloorToInt(pos.x);
        int checkZ = Mathf.FloorToInt(pos.z);
        int startY = Mathf.FloorToInt(pos.y + 0.5f);

        for (int y = startY; y >= startY - 4; y--)
        {
            if (ctx.world.GetBlock(new Vector3(checkX, y, checkZ)) != null)
            {
                return y + 1.0f; // Return Top of Block
            }
        }

        return pos.y - 10f; // Abyss
    }

    public override void Exit(PawnContext ctx) { }
}