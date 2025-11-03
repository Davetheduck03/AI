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
        PathNode nodeBelow = GetNodeBelow(transform.position + Vector3.up * 1f, nodeLayer);
        if (nodeBelow == null)
        {
            Debug.Log("Suc cac");
            return;
        }

        if (!nodeValue.ContainsKey(nodeType))
        {
            nodeValue.Add(nodeType, new List<PathNode>());
        }

        nodeValue[nodeType].Add(nodeBelow);
            
    }

    public static PathNode GetNodeBelow(Vector3 pos, LayerMask nodeLayer)
    {
        Ray ray = new Ray(pos, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 2, nodeLayer))
        {
            return hit.collider.GetComponent<PathNode>();
        }

        Collider[] hits = Physics.OverlapSphere(pos, 1f, nodeLayer);
        Debug.Log(hits.Length);
        foreach (var h in hits)
        {
            var node = h.GetComponent<PathNode>();
            if (node != null)
                return node;
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.down * 2f);
    }
}