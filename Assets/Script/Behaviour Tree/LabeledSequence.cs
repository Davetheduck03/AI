/// <summary>
/// A Sequence that writes its <paramref name="label"/> to bb["debugPhase"] whenever it
/// is the branch that is actually executing (returns Running or Success).
///
/// How it works inside the debug overlay
/// ──────────────────────────────────────
/// The root Selector is fully reactive — it re-evaluates every child from Priority 0 on
/// every BT tick.  Sequences that fail fast (guards not met) do NOT write the phase.
/// Only the first sequence that returns Running or Success writes its label, which is
/// always the active branch by definition.
///
/// Non-reactive resume: when a LabeledSequence is Running on child N, the next tick
/// Sequence.Evaluate() skips children 0..N-1 and resumes at child N.  base.Evaluate()
/// still returns Running, so the label is re-written every tick — the overlay never
/// shows a stale phase from a previously active branch.
/// </summary>
public class LabeledSequence : Sequence
{
    private readonly string _label;

    public LabeledSequence(Blackboard bb, string label) : base(bb)
    {
        _label  = label;
        name    = label;   // visible in any future tree-print debug tools
    }

    public override NodeState Evaluate()
    {
        NodeState result = base.Evaluate();

        // Write only when this branch is doing work.
        // Failure means guards did not pass — don't overwrite the active branch's label.
        if (result != NodeState.Failure)
            bb.Set("debugPhase", _label);

        return result;
    }
}
