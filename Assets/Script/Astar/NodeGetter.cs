using System.Collections.Generic;
using TowerDefenseTK;
using UnityEngine;

public enum NodeType
{
    Start,
    End
}

public class NodeGetter : MonoBehaviour
{
    [SerializeField] private NodeType nodeType;
    public static Dictionary<NodeType,List<PathNode>> nodeValue = new Dictionary<NodeType, List<PathNode>>();
    [SerializeField] private LayerMask nodeLayer;

    private void OnEnable()
    {
        GridGenerator.OnGridGenerated += Init;
    }

    private void OnDisable()
    {
        GridGenerator.OnGridGenerated -= Init;
    }

    private void Init()
    {
        PathNode nodeBelow = GetNodeBelow(transform.position + Vector3.forward * 1f, nodeLayer);
        if (nodeBelow == null)
        {
            Debug.Log("node below not found");
            return;
        }

        if (!nodeValue.ContainsKey(nodeType))
        {
            nodeValue.Add(nodeType, new List<PathNode>());
        }

        nodeValue[nodeType].Add(nodeBelow);
            
    }

    public static PathNode GetNodeBelow(Vector2 pos, LayerMask nodeLayer)
    {
        Ray ray = new Ray(pos, Vector3.back);
        if (Physics.Raycast(ray, out RaycastHit hit, 2, nodeLayer))
        {
            return hit.collider.GetComponent<PathNode>();
        }

        Collider[] hits = Physics.OverlapSphere(pos, 0.5f, nodeLayer);
        Debug.Log(hits.Length);
        foreach (var h in hits)
        {
            var node = h.GetComponent<PathNode>();
            if (node != null)
                return node;
        }
        return null;
    }

}