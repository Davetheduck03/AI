/// <summary>
/// CONDITION: Checks if knight is in combat.
/// Returns Failure if in combat (forces selector to retry attack priority).
/// Returns Success if safe (allows exploration/looting).
/// </summary>
public class CheckCombatCooldown : Node
{
    private KnightAI knightAI;

    public CheckCombatCooldown(Blackboard bb, KnightAI knight) : base(bb)
    {
        knightAI = knight;
    }

    public override NodeState Evaluate()
    {
        if (knightAI.IsInCombat())
        {
            // In combat - fail this node to force priority back to attack
            return NodeState.Failure;
        }

        // Safe - allow exploration/looting
        return NodeState.Success;
    }
}








