using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Pawn/State Definition")]
public class PawnStateSO : ScriptableObject
{
    public PawnStateBehaviour Behaviour;
    public PawnTransition[] Transitions;
}