using System.Collections.Generic;

public enum NodeState { Success, Failure, Running }

public abstract class Node
{
    protected Blackboard bb;
    protected List<Node> children = new List<Node>();
    public string name;

    protected Node(Blackboard blackboard)
    {
        bb = blackboard;
        name = GetType().Name;
    }

    public void AddChild(Node child) => children.Add(child);
    public virtual NodeState Evaluate() => NodeState.Failure;
}