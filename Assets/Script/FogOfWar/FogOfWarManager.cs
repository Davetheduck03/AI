using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// Enhanced FogOfWarManager with Field of View support.
/// Can reveal fog in circular or cone-shaped patterns.
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
    [SerializeField] private float explorationSpacing = 8f;
    [SerializeField] private int maxFailedSearches = 3;

    private Dictionary<Vector3Int, bool> revealedTiles = new Dictionary<Vector3Int, bool>();
    private HashSet<Vector3Int> allTilePositions = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> walkableTilePositions = new HashSet<Vector3Int>();

    private List<Vector3> recentTargets = new List<Vector3>();
    private const int maxRecentTargets = 5;
    private int consecutiveFailedSearches = 0;

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

        BoundsInt wallsBounds = wallsTilemap.cellBounds;
        foreach (Vector3Int pos in wallsBounds.allPositionsWithin)
        {
            if (wallsTilemap.HasTile(pos))
            {
                allTilePositions.Add(pos);
                PlaceFogTile(pos);
            }
        }

        Debug.Log($"Fog initialized: {allTilePositions.Count} total ({walkableTilePositions.Count} walkable)");
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

    /// <summary>
    /// Original circular reveal method.
    /// </summary>
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

                if (distance <= visionRadius && VisionUtilities.HasLineOfSight(worldPosition, checkWorldPos, visionBlockingLayers))
                {
                    RemoveFogTile(checkPos);
                }
            }
        }
    }

    /// <summary>
    /// NEW: Reveal fog in a cone/sector (Field of View).
    /// </summary>
    public void RevealFogInCone(Vector3 worldPosition, Vector2 facingDirection, float range, float fovAngle)
    {
        Vector3Int centerCell = fogTilemap.WorldToCell(worldPosition);
        int radius = Mathf.CeilToInt(range);
        float halfAngle = fovAngle / 2f;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector3Int checkPos = centerCell + new Vector3Int(x, y, 0);

                if (!allTilePositions.Contains(checkPos)) continue;
                if (revealedTiles.TryGetValue(checkPos, out bool isRevealed) && isRevealed) continue;

                Vector3 checkWorldPos = fogTilemap.GetCellCenterWorld(checkPos);
                Vector2 offset = checkWorldPos - worldPosition;
                float distance = offset.magnitude;

                // Check range
                if (distance > range) continue;

                // Check if within FOV angle
                if (distance > 0.1f)  // Skip angle check for center tile
                {
                    float angleToPoint = Vector2.Angle(facingDirection, offset);
                    if (angleToPoint > halfAngle) continue;
                }

                // Check line of sight
                if (VisionUtilities.HasLineOfSight(worldPosition, checkWorldPos, visionBlockingLayers))
                {
                    RemoveFogTile(checkPos);
                }
            }
        }
    }

    public bool IsRevealed(Vector3 worldPosition)
    {
        Vector3Int cellPos = fogTilemap.WorldToCell(worldPosition);
        return revealedTiles.TryGetValue(cellPos, out bool revealed) && revealed;
    }

    public Vector3? GetNearestUnrevealedPosition(Vector3 fromPosition)
    {
        Vector3? nearest = GetNearestUnrevealedWithSpacing(fromPosition, true);

        if (nearest.HasValue)
        {
            consecutiveFailedSearches = 0;
            return nearest;
        }

        consecutiveFailedSearches++;

        if (consecutiveFailedSearches >= maxFailedSearches)
        {
            Debug.Log("Clearing recent targets!");
            ClearRecentTargets();
            consecutiveFailedSearches = 0;
            return GetNearestUnrevealedWithSpacing(fromPosition, false);
        }

        return GetNearestUnrevealedWithSpacing(fromPosition, false);
    }

    private Vector3? GetNearestUnrevealedWithSpacing(Vector3 fromPosition, bool useSpacing)
    {
        Vector3? nearest = null;
        float closestDist = float.MaxValue;

        foreach (var kvp in revealedTiles)
        {
            if (kvp.Value) continue;
            if (!walkableTilePositions.Contains(kvp.Key)) continue;

            Vector3 worldPos = fogTilemap.GetCellCenterWorld(kvp.Key);

            if (useSpacing && IsTooCloseToRecentTarget(worldPos)) continue;

            float dist = Vector3.Distance(fromPosition, worldPos);

            if (dist < closestDist)
            {
                closestDist = dist;
                nearest = worldPos;
            }
        }

        return nearest;
    }

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

    public void RegisterExplorationTarget(Vector3 targetPosition)
    {
        recentTargets.Add(targetPosition);

        if (recentTargets.Count > maxRecentTargets)
        {
            recentTargets.RemoveAt(0);
        }
    }

    public void ClearRecentTargets()
    {
        recentTargets.Clear();
        consecutiveFailedSearches = 0;
    }

    public int GetUnrevealedCount()
    {
        int count = 0;
        foreach (var kvp in revealedTiles)
        {
            if (!kvp.Value && walkableTilePositions.Contains(kvp.Key))
            {
                count++;
            }
        }
        return count;
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
}