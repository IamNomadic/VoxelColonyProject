using System;

[Serializable]
public struct PawnTransition
{
    public PawnConditionSO[] Conditions; // All must be true to transition
    public PawnStateSO NextState;
}