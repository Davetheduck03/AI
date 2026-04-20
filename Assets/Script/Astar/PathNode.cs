using System.Collections.Generic;
using UnityEngine;

public class PathNode : MonoBehaviour
{
    public static event System.Action<PathNode> OnNodeUpdated;

    public Vector2Int gridPosition;
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

    /// <summary>
    /// True when this walkable tile sits at the inside of an L-shaped wall junction
    /// (i.e. two orthogonally-adjacent wall tiles meet at a 90° angle around this tile).
    /// Set once by GridGenerator.LinkNeighbors after every grid rebuild.
    ///
    /// Inner-corner tiles are usable by A* but are dead-ends for physics-driven
    /// movement: a hero pushed into one by SeparationBehavior can be wedged with
    /// no room to re-path outward.  Other systems (KiteAndAttack, SeparationBehavior)
    /// use this flag to avoid nominating these tiles as dodge / push destinations.
    /// </summary>
    public bool isInnerCorner;

    /// <summary>
    /// Number of orthogonal (cardinal) neighbors that are absent or non-walkable.
    /// Cached by GridGenerator for fast wall-proximity queries.
    /// </summary>
    public int wallNeighborCount;
}
