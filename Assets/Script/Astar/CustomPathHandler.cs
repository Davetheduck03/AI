using UnityEngine;
using System.Collections.Generic;
using TowerDefenseTK;
using System.Linq;
using Unity.VisualScripting;

public class CustomPathHandler : MonoBehaviour
{
    public List<PathNode> pathNodes;
    private PathNode startNode;
    private PathNode endNode;
    public Transform endGoal;
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
        var nodeGetterComponent = this.gameObject.GetComponent<NodeGetter>();
        startNode = NodeGetter.GetNodeBelow(transform.position + Vector3.up * 1f, nodeLayer);
        endNode = NodeGetter.GetNodeBelow(endGoal.position + Vector3.up * 1f, nodeLayer);
        pathNodes.Add(endNode);
        pathNodes.Insert(0, startNode);
        Astar.Instance.customPathCache.Add((startNode, endNode), pathNodes);
    }
}
