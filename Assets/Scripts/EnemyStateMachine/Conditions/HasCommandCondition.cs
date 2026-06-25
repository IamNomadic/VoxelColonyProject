using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Conditions/Has Command")]
public class Condition_HasOrders : PawnConditionSO
{
    [Tooltip("If true, returns True when queue is EMPTY.")]
    public bool invert = false;

    public override bool Evaluate(PawnContext ctx)
    {
        // FIX: Check the NEW 'orderQueue', not the old 'commandQueue'
        bool hasOrders = (ctx.currentOrder != null) || (ctx.orderQueue.Count > 0);

        return invert ? !hasOrders : hasOrders;
    }
}