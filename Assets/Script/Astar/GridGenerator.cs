using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridGenerator : MonoBehaviour
{
    public static event Action OnGridGenerated;
    public static GridGenerator Instance { get; private set; }

    [Header("Tilemap Settings")]
    public Tilemap walkableTilemap;

    [Header("Node Settings")]
    public GameObject nodePrefab;
    public float cellSize = 1f;

    private PathNode[,] grid;
    private Vector2Int gridOffset;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Grid is built by RegenerateGrid() after the dungeon tiles are painted,
    // not at startup — there are no tiles to grid until Confirm() runs.

    public void GenerateGrid()
    {
        if (walkableTilemap == null)
        {
            Debug.LogError("Walkable Tilemap is not assigned!");
            return;
        }

        BoundsInt bounds = walkableTilemap.cellBounds;
        gridOffset = new Vector2Int(bounds.xMin, bounds.yMin);

        int width = bounds.size.x;
        int height = bounds.size.y;
        grid = new PathNode[width, height];

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cellPos = new Vector3Int(x, y, 0);
                TileBase tile = walkableTilemap.GetTile(cellPos);

                if (tile == null)
                    continue;

                int gridX = x - bounds.xMin;
                int gridY = y - bounds.yMin;

                Vector3 worldPos = walkableTilemap.GetCellCenterWorld(cellPos);

                GameObject nodeObj = Instantiate(nodePrefab, worldPos, Quaternion.identity, transform);
                nodeObj.name = $"Node ({x},{y})";
                PathNode node = nodeObj.GetComponent<PathNode>();
                node.gridPosition = new Vector2Int(x, y);
                node.isWalkable   = true;
                node.gCost        = Mathf.Infinity;   // sentinel for A* lazy-reset
                node.parent       = null;
                grid[gridX, gridY] = node;
                Astar.Instance.allNodes.Add(node);
            }
        }
    }

    /// <summary>
    /// Gets nearest walkable node using efficient spiral search from grid position.
    /// </summary>
    public PathNode GetNearestWalkableNode(Vector3 worldPos, int maxSearchRadius = 20)
    {
        Vector3Int startCell = walkableTilemap.WorldToCell(worldPos);

        PathNode directNode = GetNodeAt(startCell.x, startCell.y);
        if (directNode != null && directNode.isWalkable)
            return directNode;

        for (int radius = 1; radius <= maxSearchRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                        continue;

                    int checkX = startCell.x + dx;
                    int checkY = startCell.y + dy;

                    PathNode node = GetNodeAt(checkX, checkY);
                    if (node != null && node.isWalkable)
                        return node;
                }
            }
        }

        Debug.LogWarning($"No walkable node found within {maxSearchRadius} tiles of {worldPos}");
        return null;
    }

    /// <summary>
    /// Destroys all existing PathNode objects, clears A*'s node list, then
    /// re-reads the (now repainted) walkable tilemap to build a fresh grid.
    /// Call this after DungeonGenerator.Regenerate() has painted new tiles.
    /// </summary>
    public void RegenerateGrid()
    {
        // Destroy all existing node GameObjects
        foreach (var node in Astar.Instance.allNodes)
            if (node != null) Destroy(node.gameObject);

        Astar.Instance.allNodes.Clear();
        grid = null;

        GenerateGrid();
        LinkNeighbors();
        StartCoroutine(WaitTillEndOfFrame());
    }

    private IEnumerator WaitTillEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        OnGridGenerated?.Invoke();
    }

    private void LinkNeighbors()
    {
        if (grid == null)
            return;

        int width  = grid.GetLength(0);
        int height = grid.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                PathNode node = grid[x, y];

                if (node == null)
                    continue;

                node.neighbors.Clear();

                // ── Cardinal neighbours ───────────────────────────────────────
                if (x > 0          && grid[x - 1, y    ] != null) node.neighbors.Add(grid[x - 1, y    ]);
                if (x < width  - 1 && grid[x + 1, y    ] != null) node.neighbors.Add(grid[x + 1, y    ]);
                if (y > 0          && grid[x,     y - 1 ] != null) node.neighbors.Add(grid[x,     y - 1]);
                if (y < height - 1 && grid[x,     y + 1 ] != null) node.neighbors.Add(grid[x,     y + 1]);

                // ── Diagonal neighbours ───────────────────────────────────────
                // Corner-cutting is forbidden: a diagonal step is only added when
                // BOTH adjacent cardinal tiles exist (are walkable floor tiles).
                // This prevents heroes from squeezing through the shared corner of
                // two wall tiles in narrow dungeon corridors.

                // SW  (x-1, y-1)
                if (x > 0 && y > 0 &&
                    grid[x - 1, y    ] != null && grid[x, y - 1] != null &&
                    grid[x - 1, y - 1] != null)
                    node.neighbors.Add(grid[x - 1, y - 1]);

                // SE  (x+1, y-1)
                if (x < width - 1 && y > 0 &&
                    grid[x + 1, y    ] != null && grid[x, y - 1] != null &&
                    grid[x + 1, y - 1] != null)
                    node.neighbors.Add(grid[x + 1, y - 1]);

                // NW  (x-1, y+1)
                if (x > 0 && y < height - 1 &&
                    grid[x - 1, y    ] != null && grid[x, y + 1] != null &&
                    grid[x - 1, y + 1] != null)
                    node.neighbors.Add(grid[x - 1, y + 1]);

                // NE  (x+1, y+1)
                if (x < width - 1 && y < height - 1 &&
                    grid[x + 1, y    ] != null && grid[x, y + 1] != null &&
                    grid[x + 1, y + 1] != null)
                    node.neighbors.Add(grid[x + 1, y + 1]);
            }
        }
    }

    public PathNode GetNodeAt(int tilemapX, int tilemapY)
    {
        int gridX = tilemapX - gridOffset.x;
        int gridY = tilemapY - gridOffset.y;

        if (gridX < 0 || gridY < 0 || gridX >= grid.GetLength(0) || gridY >= grid.GetLength(1))
            return null;

        return grid[gridX, gridY];
    }

    public PathNode GetNodeAtWorldPosition(Vector3 worldPos)
    {
        Vector3Int cellPos = walkableTilemap.WorldToCell(worldPos);
        return GetNodeAt(cellPos.x, cellPos.y);
    }
}
