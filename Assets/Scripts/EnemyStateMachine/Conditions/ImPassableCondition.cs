using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/Conditions/ImPassableCondition", fileName = "ImPassableCondition")]
public class ImPassableCondition : PawnConditionSO
{
    public override bool Evaluate(PawnContext ctx)
    {
        return false;
    }
}
