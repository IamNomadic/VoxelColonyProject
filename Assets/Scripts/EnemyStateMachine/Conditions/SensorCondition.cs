using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Conditions/Sensor Check")]
public class SensorCondition : PawnConditionSO
{
    [Tooltip("What are we looking for?")]
    public PawnType targetType;

    [Tooltip("If true, this condition passes when the target is NOT found.")]
    public bool reverse;

    public override bool Evaluate(PawnContext ctx)
    {
        Transform t = ctx.ScanForTarget(targetType);
        bool found = (t != null);

        // If reverse is true (e.g., "Is Safe?"), we return true if NOT found
        return reverse ? !found : found;
    }
}