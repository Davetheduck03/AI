using System.Collections.Generic;
using UnityEngine;

public class PathNode : MonoBehaviour
{
    public static event System.Action<PathNode> OnNodeUpdated;

    public Vector2Int gridPosition;
    public bool isPlaceable = true;
    public List<PathNode> neighbors = new List<PathNode>();

    [HideInInspector] public float gCost;
    [HideInInspector] public float hCost;
    [HideInInspector] public float fCost => gCost + hCost;
    [HideInInspector] public PathNode parent;

    private bool _isWalkable = true;
    public bool isWalkable
    {
        get => _isWalkable;
        set
        {
            if (_isWalkable != value)
            {
                _isWalkable = value;
                OnNodeUpdated?.Invoke(this);
            }
        }

    }
}