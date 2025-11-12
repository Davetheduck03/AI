using UnityEngine;

public class NodeGetter : MonoBehaviour
{
    public static NodeGetter Instance;

    private void Awake()
    {
        Instance = this;
    }

    public PathNode GetClosestNode(Vector3 worldPos)
    {
        PathNode closest = null;
        float bestDist = Mathf.Infinity;
        Vector2 pos2D = (Vector2)worldPos;

        foreach (var node in Astar.Instance.allNodes)
        {
            if (!node.isWalkable) continue;
            float dist = Vector2.Distance(pos2D, (Vector2)node.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                closest = node;
            }
        }

        return closest;
    }
}