using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Conditions/Has Command")]
public class Condition_HasCommand : PawnConditionSO
{
    [Tooltip("If true, returns True when commands exist. If false, returns True when queue is empty.")]
    public bool invert = false;

    public override bool Evaluate(PawnContext ctx)
    {
        // We have a command if we are currently moving to one OR if there are more in the queue
        bool hasCommand = (ctx.currentCommandTarget != null) || (ctx.commandQueue.Count > 0);

        return invert ? !hasCommand : hasCommand;
    }
}