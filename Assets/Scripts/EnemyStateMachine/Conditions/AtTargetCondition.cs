using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Conditions/At Target")]
public class Cond_AtTarget : PawnConditionSO
{
    public float range = 1.5f; // Interaction range

    public override bool Evaluate(PawnContext ctx)
    {
        if (ctx.currentOrder == null) return false;

        Vector3 target = ctx.currentOrder.Value.target;
        // Ignore Y for easier checking, or keep it strict
        float dist = Vector3.Distance(ctx.transform.position, target);

        return dist <= range;
    }
}