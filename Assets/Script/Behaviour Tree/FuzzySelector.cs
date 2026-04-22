using System;
using System.Collections.Generic;

/// <summary>
/// A Selector variant that scores its children before choosing which one to run.
/// Each child is registered alongside a score function (0–1).
/// Every tick the node calls all score functions, picks the child with the highest
/// score, and delegates to it.
///
/// Unlike a standard priority Selector (which always tries children in fixed order),
/// FuzzySelector lets situational weights shift which branch is "most important"
/// right now — enabling smooth, context-sensitive decision-making.
///
/// USAGE:
///   var fuzzy = new FuzzySelector(bb);
///   fuzzy.Add(healCritSeq,   () => FuzzyLogic.RampDown(lowestHPFraction, 0f, 0.35f));
///   fuzzy.Add(healNormSeq,   () => FuzzyLogic.RampDown(lowestHPFraction, 0.35f, 0.75f));
///   root.AddChild(fuzzy);
///
/// NOTE: If all children score 0 the node returns Failure so the parent Selector
/// can fall through to lower-priority alternatives.
/// </summary>
public class FuzzySelector : Node
{
    private readonly List<(Node node, Func<float> scoreFunc)> _children = new();

    public FuzzySelector(Blackboard bb) : base(bb) { }

    /// <summary>
    /// Registers a child node with its scoring function.
    /// Returns this so calls can be chained.
    /// </summary>
    public FuzzySelector Add(Node child, Func<float> scoreFunc)
    {
        _children.Add((child, scoreFunc));
        return this;
    }

    public override NodeState Evaluate()
    {
        Node  best      = null;
        float bestScore = -1f;

        foreach (var (node, scoreFunc) in _children)
        {
            float s = scoreFunc?.Invoke() ?? 0f;
            if (s > bestScore)
            {
                bestScore = s;
                best      = node;
            }
        }

        // All children scored 0 (or no children) — nothing to do
        if (best == null || bestScore <= 0f)
            return NodeState.Failure;

        return best.Evaluate();
    }
}
