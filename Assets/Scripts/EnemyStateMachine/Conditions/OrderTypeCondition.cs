using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Conditions/Check Order Type")]
public class Cond_OrderType : PawnConditionSO
{
    public OrderType typeToCheck;

    public override bool Evaluate(PawnContext ctx)
    {
        if (ctx.currentOrder == null) return false;
        return ctx.currentOrder.Value.type == typeToCheck;
    }
}