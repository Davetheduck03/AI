using System.Collections.Generic;
using TowerDefenseTK;
using UnityEngine;


public class NodeGetter : MonoBehaviour
{
    [SerializeField] private LayerMask nodeLayer;

    public static PathNode GetClosestNode(Vector3 pos, LayerMask nodeLayer)
    {
        Collider[] hits = Physics.OverlapSphere(pos, 1f, nodeLayer);
        print(hits.Length);
        PathNode closest = null;
        float closestSqrDist = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            PathNode node = hit.GetComponent<PathNode>();
            if (node == null) continue;

            float sqrDist = (node.transform.position - pos).sqrMagnitude;
            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = node;
            }
        }
        return closest;
    }

}