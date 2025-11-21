using UnityEngine;

// The base class for all your Logic (Wander, Idle, Attack, etc.)
public abstract class PawnStateBehaviour : ScriptableObject, IPawnState
{
    public abstract void Enter(PawnContext ctx);
    public abstract void Execute(PawnContext ctx);
    public abstract void Exit(PawnContext ctx);
}