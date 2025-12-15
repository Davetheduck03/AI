using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// FogOfWarManager - Handles fog placement and revealing.
/// Walls BLOCK vision (can't see through) but REVEAL when close (walk near).
/// </summary>
public class FogOfWarManager : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap walkableTilemap;
    [SerializeField] private Tilemap wallsTilemap;
    [SerializeField] private Tilemap fogTilemap;

    [Header("Fog Appearance")]
    [SerializeField] private TileBase fogTile;
    [SerializeField] private Color fogColor = new Color(0, 0, 0, 0.8f);

    [Header("Wall Reveal Settings")]
    [SerializeField] private float wallRevealDistance = 1.5f;  // How close to reveal walls

    [Header("Exploration Settings")]
    [SerializeField] private float explorationSpacing = 8f;
    [SerializeField] private int maxFailedSearches = 3;

    // Tile tracking
    private Dictionary<Vector3Int, bool> revealedTiles = new Dictionary<Vector3Int, bool>();
    private HashSet<Vector3Int> allTilePositions = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> walkableTilePositions = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> wallTilePositions = new HashSet<Vector3Int>();

    // Exploration tracking
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
        wallTilePositions.Clear();

        // Add fog to walkable tiles
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

        // Add fog to wall tiles
        BoundsInt wallsBounds = wallsTilemap.cellBounds;
        foreach (Vector3Int pos in wallsBounds.allPositionsWithin)
        {
            if (wallsTilemap.HasTile(pos))
            {
                allTilePositions.Add(pos);
                wallTilePositions.Add(pos);
                PlaceFogTile(pos);
            }
        }

        Debug.Log($"Fog initialized: {allTilePositions.Count} total tiles " +
                  $"({walkableTilePositions.Count} walkable, {wallTilePositions.Count} walls)");
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
    /// Reveal fog around a position.
    /// Walls BLOCK line of sight but REVEAL if within wallRevealDistance.
    /// </summary>
    public void RevealFogAroundPosition(Vector3 worldPosition, float radius, LayerMask visionBlockingLayers)
    {
        Vector3Int centerCell = fogTilemap.WorldToCell(worldPosition);
        int checkRadius = Mathf.CeilToInt(radius);

        for (int x = -checkRadius; x <= checkRadius; x++)
        {
            for (int y = -checkRadius; y <= checkRadius; y++)
            {
                Vector3Int checkPos = centerCell + new Vector3Int(x, y, 0);

                // Must be a valid tile
                if (!allTilePositions.Contains(checkPos)) continue;

                // Skip if already revealed
                if (revealedTiles.TryGetValue(checkPos, out bool isRevealed) && isRevealed) continue;

                Vector3 checkWorldPos = fogTilemap.GetCellCenterWorld(checkPos);
                float distance = Vector2.Distance(worldPosition, checkWorldPos);

                // Check if in range
                if (distance > radius) continue;

                bool isWall = wallTilePositions.Contains(checkPos);

                if (isWall)
                {
                    // WALLS: Reveal only if very close (ignore line of sight for walls themselves)
                    if (distance <= wallRevealDistance)
                    {
                        RemoveFogTile(checkPos);
                    }
                }
                else
                {
                    // WALKABLE TILES: Check line of sight (walls block this)
                    if (VisionUtilities.HasLineOfSight(worldPosition, checkWorldPos, visionBlockingLayers))
                    {
                        RemoveFogTile(checkPos);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reveal fog in a cone/sector (Field of View).
    /// Walls block vision but reveal when close.
    /// </summary>
    public void RevealFogInCone(Vector3 worldPosition, Vector2 facingDirection, float range,
                                float fovAngle, LayerMask visionBlockingLayers)
    {
        Vector3Int centerCell = fogTilemap.WorldToCell(worldPosition);
        int checkRadius = Mathf.CeilToInt(range);
        float halfAngle = fovAngle / 2f;

        for (int x = -checkRadius; x <= checkRadius; x++)
        {
            for (int y = -checkRadius; y <= checkRadius; y++)
            {
                Vector3Int checkPos = centerCell + new Vector3Int(x, y, 0);

                // Must be valid tile
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

                bool isWall = wallTilePositions.Contains(checkPos);

                if (isWall)
                {
                    // WALLS: Reveal only if very close
                    if (distance <= wallRevealDistance)
                    {
                        RemoveFogTile(checkPos);
                    }
                }
                else
                {
                    // WALKABLE TILES: Check line of sight
                    if (VisionUtilities.HasLineOfSight(worldPosition, checkWorldPos, visionBlockingLayers))
                    {
                        RemoveFogTile(checkPos);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check if a position is revealed (no fog).
    /// </summary>
    public bool IsRevealed(Vector3 worldPosition)
    {
        Vector3Int cellPos = fogTilemap.WorldToCell(worldPosition);
        return revealedTiles.TryGetValue(cellPos, out bool revealed) && revealed;
    }

    /// <summary>
    /// Get nearest unrevealed WALKABLE position for exploration.
    /// </summary>
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
            // Skip revealed tiles
            if (kvp.Value) continue;

            // Only return WALKABLE unrevealed tiles
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

    /// <summary>
    /// Get count of unrevealed WALKABLE tiles.
    /// </summary>
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

    /// <summary>
    /// Get list of unrevealed WALKABLE positions.
    /// </summary>
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

    /// <summary>
    /// Check if a world position is on a walkable tile.
    /// </summary>
    public bool IsWalkable(Vector3 worldPosition)
    {
        Vector3Int cellPos = fogTilemap.WorldToCell(worldPosition);
        return walkableTilePositions.Contains(cellPos);
    }

    /// <summary>
    /// Get total fog coverage percentage.
    /// </summary>
    public float GetFogCoveragePercent()
    {
        if (allTilePositions.Count == 0) return 0f;

        int revealedCount = 0;
        foreach (var kvp in revealedTiles)
        {
            if (kvp.Value) revealedCount++;
        }

        return (revealedCount / (float)allTilePositions.Count) * 100f;
    }
}