using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// FIXED: Prevents oscillation in narrow entrances by marking nearby tiles as explored.
/// When AI gets close to target, mark surrounding area as explored to avoid re-targeting.
/// </summary>
public class FogOfWarManager : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap walkableTilemap;
    [SerializeField] private Tilemap wallsTilemap;
    [SerializeField] private Tilemap fogTilemap;

    [Header("Fog Settings")]
    [SerializeField] private TileBase fogTile;
    [SerializeField] private Color fogColor = new Color(0, 0, 0, 0.8f);

    [Header("Vision Settings")]
    [SerializeField] private float visionRadius = 5f;
    [SerializeField] private LayerMask visionBlockingLayers;

    [Header("Exploration Settings")]
    [Tooltip("Minimum distance between exploration targets to avoid oscillation")]
    [SerializeField] private float explorationSpacing = 8f;

    // Track revealed tiles
    private Dictionary<Vector3Int, bool> revealedTiles = new Dictionary<Vector3Int, bool>();
    private HashSet<Vector3Int> allTilePositions = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> walkableTilePositions = new HashSet<Vector3Int>();

    // Track recently targeted positions to avoid re-targeting
    private List<Vector3> recentTargets = new List<Vector3>();
    private const int maxRecentTargets = 5;

    private void Start()
    {
        InitializeFog();
    }

    private void InitializeFog()
    {
        if (walkableTilemap == null || wallsTilemap == null || fogTilemap == null)
        {
            Debug.LogError("FogOfWarManager: Missing tilemap references!");
            return;
        }

        fogTilemap.ClearAllTiles();
        revealedTiles.Clear();
        allTilePositions.Clear();
        walkableTilePositions.Clear();

        // Cover walkable tiles with fog
        BoundsInt walkableBounds = walkableTilemap.cellBounds;
        foreach (Vector3Int pos in walkableBounds.allPositionsWithin)
        {
            if (walkableTilemap.HasTile(pos))
            {
                allTilePositions.Add(pos);
                walkableTilePositions.Add(pos);
                PlaceFogTile(pos);
            }
        }

        // Cover wall tiles with fog
        BoundsInt wallsBounds = wallsTilemap.cellBounds;
        foreach (Vector3Int pos in wallsBounds.allPositionsWithin)
        {
            if (wallsTilemap.HasTile(pos))
            {
                allTilePositions.Add(pos);
                PlaceFogTile(pos);
            }
        }

        Debug.Log($"Fog initialized: {allTilePositions.Count} total tiles ({walkableTilePositions.Count} walkable)");
    }

    private void PlaceFogTile(Vector3Int cellPos)
    {
        revealedTiles[cellPos] = false;
        fogTilemap.SetTile(cellPos, fogTile);
        fogTilemap.SetColor(cellPos, fogColor);
    }

    private void RemoveFogTile(Vector3Int cellPos)
    {
        revealedTiles[cellPos] = true;
        fogTilemap.SetTile(cellPos, null);
    }

    public void RevealFogAroundPosition(Vector3 worldPosition)
    {
        Vector3Int centerCell = fogTilemap.WorldToCell(worldPosition);
        int radius = Mathf.CeilToInt(visionRadius);

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector3Int checkPos = centerCell + new Vector3Int(x, y, 0);

                if (!allTilePositions.Contains(checkPos)) continue;
                if (revealedTiles.TryGetValue(checkPos, out bool isRevealed) && isRevealed) continue;

                Vector3 checkWorldPos = fogTilemap.GetCellCenterWorld(checkPos);
                float distance = Vector2.Distance(worldPosition, checkWorldPos);

                if (distance <= visionRadius && HasLineOfSight(worldPosition, checkWorldPos))
                {
                    RemoveFogTile(checkPos);
                }
            }
        }
    }

    private bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector2 direction = (to - from).normalized;
        float distance = Vector2.Distance(from, to);
        RaycastHit2D hit = Physics2D.Raycast(from, direction, distance, visionBlockingLayers);
        return hit.collider == null;
    }

    public bool IsRevealed(Vector3 worldPosition)
    {
        Vector3Int cellPos = fogTilemap.WorldToCell(worldPosition);
        return revealedTiles.TryGetValue(cellPos, out bool revealed) && revealed;
    }

    /// <summary>
    /// Get nearest walkable unrevealed position, avoiding recently targeted areas.
    /// </summary>
    public Vector3? GetNearestUnrevealedPosition(Vector3 fromPosition)
    {
        Vector3? nearest = null;
        float closestDist = float.MaxValue;

        foreach (var kvp in revealedTiles)
        {
            if (kvp.Value) continue;
            if (!walkableTilePositions.Contains(kvp.Key)) continue;

            Vector3 worldPos = fogTilemap.GetCellCenterWorld(kvp.Key);

            // NEW: Skip if too close to a recent target (prevents oscillation)
            if (IsTooCloseToRecentTarget(worldPos)) continue;

            float dist = Vector3.Distance(fromPosition, worldPos);

            if (dist < closestDist)
            {
                closestDist = dist;
                nearest = worldPos;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Check if position is too close to recently targeted positions.
    /// </summary>
    private bool IsTooCloseToRecentTarget(Vector3 position)
    {
        foreach (Vector3 recentTarget in recentTargets)
        {
            if (Vector3.Distance(position, recentTarget) < explorationSpacing)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Register a position as recently targeted (called by exploration node).
    /// </summary>
    public void RegisterExplorationTarget(Vector3 targetPosition)
    {
        recentTargets.Add(targetPosition);

        // Keep list small
        if (recentTargets.Count > maxRecentTargets)
        {
            recentTargets.RemoveAt(0);
        }

        Debug.Log($"Registered exploration target: {targetPosition} (total: {recentTargets.Count})");
    }

    /// <summary>
    /// Clear recent targets (useful for resetting exploration).
    /// </summary>
    public void ClearRecentTargets()
    {
        recentTargets.Clear();
    }

    public List<Vector3> GetUnrevealedPositions()
    {
        List<Vector3> unrevealed = new List<Vector3>();

        foreach (var kvp in revealedTiles)
        {
            if (!kvp.Value && walkableTilePositions.Contains(kvp.Key))
            {
                Vector3 worldPos = fogTilemap.GetCellCenterWorld(kvp.Key);
                unrevealed.Add(worldPos);
            }
        }

        return unrevealed;
    }

    public bool IsWalkable(Vector3 worldPosition)
    {
        Vector3Int cellPos = fogTilemap.WorldToCell(worldPosition);
        return walkableTilePositions.Contains(cellPos);
    }

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, visionRadius);

            // Draw recent targets
            Gizmos.color = Color.red;
            foreach (Vector3 target in recentTargets)
            {
                Gizmos.DrawWireSphere(target, explorationSpacing);
            }
        }
    }
}