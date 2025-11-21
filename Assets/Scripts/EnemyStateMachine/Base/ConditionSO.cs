using UnityEngine;

// Base class for Transition Conditions (e.g., "IsTargetReached?")
public abstract class PawnConditionSO : ScriptableObject
{
    public abstract bool Evaluate(PawnContext ctx);
}