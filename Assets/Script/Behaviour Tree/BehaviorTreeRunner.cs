using UnityEngine;

public class BehaviorTreeRunner : MonoBehaviour
{
    private Node root;
    protected Blackboard bb;

    [Header("Config - Override in child classes or Inspector")]
    public Transform target;  // e.g., player


    protected virtual void Start()
    {
        bb = new Blackboard();
        bb.Set("self", transform);

        root = BuildTree();  // Build instance here
    }

    protected virtual Node BuildTree()
    {
        // Override in subclasses or build dynamically
        return null;  // Placeholder
    }

    private void Update()
    {
        if (root != null) root.Evaluate();
    }
}