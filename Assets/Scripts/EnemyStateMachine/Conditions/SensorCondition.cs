using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Conditions/Can See Pawn")]
public class CanSeePawnCondition : PawnConditionSO
{
    [Tooltip("What are we looking for? (e.g. Predator looks for Prey)")]
    public PawnType targetType;

    [Tooltip("If true, we flip the result (Return true if we see NOTHING).")]
    public bool reverse;

    public override bool Evaluate(PawnContext ctx)
    {
        // 1. Use the Context's scanner (which we fixed in the last step)
        Transform target = ctx.ScanForTarget(targetType);

        // 2. Determine result
        bool found = (target != null);

        // 3. Handle 'Reverse' (e.g., used for "Lost Sight of Target")
        if (reverse) return !found;
        return found;
    }
}