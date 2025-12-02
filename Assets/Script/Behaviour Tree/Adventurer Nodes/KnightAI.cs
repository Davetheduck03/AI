using UnityEngine;

public class KnightAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask enemyLayer;

    protected override Node BuildTree()
    {
        var root = new Selector(bb);

        var attackSeq = new Sequence(bb);
        attackSeq.AddChild(new FindNearestEnemy(bb, 5f));
        attackSeq.AddChild(new MoveTowardsTarget(bb, 4f, 1f));
        attackSeq.AddChild(new IsInAttackRange(bb));
        attackSeq.AddChild(new AttackTarget(bb, 5f, 5f, 1f, enemyLayer));
        root.AddChild(attackSeq);

        var lootSeq = new Sequence(bb);
        lootSeq.AddChild(new FindLootInRange(bb, 8f));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 3 , 0f));
        root.AddChild(lootSeq);

        var wanderingSeq = new Sequence(bb);



        return root;
    }
}