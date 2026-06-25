using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Behaviors/Break Block")]
public class State_BreakBlock : PawnStateBehaviour
{
    public float breakTime = 1.0f; // Time it takes to break

    public override void Enter(PawnContext ctx)
    {
        Debug.Log("eNTER");
        ctx.stateTimer = 0;
        // Optional: Trigger "Attack" animation
        if (ctx.animator != null) ctx.animator.SetTrigger("Attack");
    }

    public override void Execute(PawnContext ctx)
    {
        ctx.stateTimer += Time.deltaTime;

        if (ctx.currentOrder == null) return;
        Vector3 targetBlock = ctx.currentOrder.Value.target;

        // Face the block
        Vector3 dir = (targetBlock - ctx.transform.position).normalized;
        dir.y = 0; // Keep upright
        if (dir != Vector3.zero)
            ctx.transform.rotation = Quaternion.LookRotation(dir);

        // Done?
        if (ctx.stateTimer >= breakTime)
        {
            Debug.Log("ajavavavsjs");
            // 1. Break the block
            // Note: We subtract a tiny bit to get 'inside' the block coordinate if the command was the face center
            // But usually the command is the exact block coordinate.
            ctx.world.ModifyBlock(targetBlock, null);

            // 2. Clear Order
            ctx.currentOrder = null;

            // 3. Effects (Optional)
            // ParticleSystem... 
        }
    }

    public override void Exit(PawnContext ctx)
    {
        // Reset animation logic if needed
    }
}