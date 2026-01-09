using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Conditions/Is Job Finished")]
public class Cond_JobFinished : PawnConditionSO
{
    public override bool Evaluate(PawnContext ctx)
    {
        // Returns TRUE if we have no active order (meaning we finished the last one)
        return ctx.currentOrder == null;
    }
}