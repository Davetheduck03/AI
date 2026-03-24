using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// FogOfWarManager - Handles fog placement and revealing.
/// Walls BLOCK vision but REVEAL when adjacent to revealed walkable tiles.
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
    [SerializeField] private float wallRevealDistance = 2.5f;

    [Header("Exploration Settings")]
    [SerializeField] private float explorationSpacing = 8f;
    [SerializeField] private int maxFailedSearches = 3;

    private Dictionary<Vector3Int, bool> revealedTiles = new Dictionary<Vector3Int, bool>();
    private HashSet<Vector3Int> allTilePositions = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> walkableTilePositions = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> wallTilePositions = new HashSet<Vector3Int>();

    private List<Vector3> recentTargets = new List<Vector3>();
    private const int maxRecentTargets = 5;
    private int consecutiveFailedSearches = 0;

    private void OnEnable()  => DungeonGenerator.OnDungeonGenerated += InitializeFog;
    private void OnDisable() => DungeonGenerator.OnDungeonGenerated -= InitializeFog;

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
                wallTilePositions.Add(pos);
                // Track wall positions for the reveal system but don't place a
                // visual fog tile on top of them — walls are always visible.
                revealedTiles[pos] = false;
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

    public void RevealFogAroundPosition(Vector3 worldPosition, float radius, LayerMask visionBlockingLayers)
    {
        Vector3Int centerCell = fogTilemap.WorldToCell(worldPosition);
        int checkRadius = Mathf.CeilToInt(radius);

        for (int x = -checkRadius; x <= checkRadius; x++)
        {
            for (int y = -checkRadius; y <= checkRadius; y++)
            {
                Vector3Int checkPos = centerCell + new Vector3Int(x, y, 0);

                if (!allTilePositions.Contains(checkPos)) continue;
                if (revealedTiles.TryGetValue(checkPos, out bool isRevealed) && isRevealed) continue;

                Vector3 checkWorldPos = fogTilemap.GetCellCenterWorld(checkPos);
                float distance = Vector2.Distance(worldPosition, checkWorldPos);

                if (distance > radius) continue;

                bool isWall = wallTilePositions.Contains(checkPos);

                if (!isWall)
                {
                    if (VisionUtilities.HasLineOfSight(worldPosition, checkWorldPos, visionBlockingLayers))
                    {
                        RemoveFogTile(checkPos);
                    }
                }
                else if (distance <= wallRevealDistance)
                {
                    RemoveFogTile(checkPos);
                }
            }
        }

        RevealAdjacentWalls();
    }

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

                if (!allTilePositions.Contains(checkPos)) continue;
                if (revealedTiles.TryGetValue(checkPos, out bool isRevealed) && isRevealed) continue;

                Vector3 checkWorldPos = fogTilemap.GetCellCenterWorld(checkPos);
                Vector2 offset = checkWorldPos - worldPosition;
                float distance = offset.magnitude;

                if (distance > range) continue;

                if (distance > 0.1f)
                {
                    float angleToPoint = Vector2.Angle(facingDirection, offset);
                    if (angleToPoint > halfAngle) continue;
                }

                bool isWall = wallTilePositions.Contains(checkPos);

                if (!isWall)
                {
                    if (VisionUtilities.HasLineOfSight(worldPosition, checkWorldPos, visionBlockingLayers))
                    {
                        RemoveFogTile(checkPos);
                    }
                }
                else if (distance <= wallRevealDistance)
                {
                    RemoveFogTile(checkPos);
                }
            }
        }

        RevealAdjacentWalls();
    }

    private void RevealAdjacentWalls()
    {
        List<Vector3Int> wallsToReveal = new List<Vector3Int>();

        foreach (Vector3Int wallPos in wallTilePositions)
        {
            if (revealedTiles.TryGetValue(wallPos, out bool isRevealed) && isRevealed)
                continue;

            if (HasAdjacentRevealedWalkable(wallPos))
            {
                wallsToReveal.Add(wallPos);
            }
        }

        foreach (Vector3Int wallPos in wallsToReveal)
        {
            RemoveFogTile(wallPos);
        }
    }

    private bool HasAdjacentRevealedWalkable(Vector3Int pos)
    {
        Vector3Int[] neighbors = new Vector3Int[]
        {
            pos + Vector3Int.up,
            pos + Vector3Int.down,
            pos + Vector3Int.left,
            pos + Vector3Int.right,
            pos + new Vector3Int(1, 1, 0),
            pos + new Vector3Int(1, -1, 0),
            pos + new Vector3Int(-1, 1, 0),
            pos + new Vector3Int(-1, -1, 0)
        };

        foreach (Vector3Int neighbor in neighbors)
        {
            if (!walkableTilePositions.Contains(neighbor))
                continue;

            if (revealedTiles.TryGetValue(neighbor, out bool revealed) && revealed)
                return true;
        }

        return false;
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