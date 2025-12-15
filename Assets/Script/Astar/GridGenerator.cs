using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridGenerator : MonoBehaviour
{
    public static event Action OnGridGenerated;
    public static GridGenerator Instance;

    [Header("Tilemap Settings")]
    public Tilemap walkableTilemap;

    [Header("Node Settings")]
    public GameObject nodePrefab;
    public float cellSize = 1f;

    private PathNode[,] grid;
    private Vector2Int gridOffset; // To handle negative tilemap positions

    private void Start()
    {
        GridGenerator.Instance = this;
        GenerateGrid();
        LinkNeighbors();
        StartCoroutine(WaitTillEndOfFrame());
    }

    public void GenerateGrid()
    {
        if (walkableTilemap == null)
        {
            Debug.LogError("Walkable Tilemap is not assigned!");
            return;
        }

        // Get the bounds of the tilemap
        BoundsInt bounds = walkableTilemap.cellBounds;

        // Store offset to handle negative positions
        gridOffset = new Vector2Int(bounds.xMin, bounds.yMin);

        // Initialize grid based on tilemap bounds
        int width = bounds.size.x;
        int height = bounds.size.y;
        grid = new PathNode[width, height];

        // Iterate through all positions in the tilemap bounds
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cellPos = new Vector3Int(x, y, 0);
                TileBase tile = walkableTilemap.GetTile(cellPos);

                // Only create nodes where tiles exist
                if (tile == null)
                    continue;

                // Convert tilemap position to grid array indices
                int gridX = x - bounds.xMin;
                int gridY = y - bounds.yMin;

                // Get world position from tilemap
                Vector3 worldPos = walkableTilemap.GetCellCenterWorld(cellPos);

                // Spawn new node
                GameObject nodeObj = Instantiate(nodePrefab, worldPos, Quaternion.identity, transform);
                nodeObj.name = $"Node ({x},{y})";
                PathNode node = nodeObj.GetComponent<PathNode>();
                node.gridPosition = new Vector2Int(x, y); // Store actual tilemap coordinates
                node.isWalkable = true;
                grid[gridX, gridY] = node;
                Astar.Instance.allNodes.Add(node);
            }
        }
    }

    public PathNode GetNearestWalkableNode(Vector3 worldPos, int maxSearchRadius = 20)
    {
        Vector3Int startCell = walkableTilemap.WorldToCell(worldPos);

        // First check if the position itself is valid
        PathNode directNode = GetNodeAt(startCell.x, startCell.y);
        if (directNode != null && directNode.isWalkable)
            return directNode;

        // Spiral outward to find nearest walkable node
        for (int radius = 1; radius <= maxSearchRadius; radius++)
        {
            // Check all positions in current radius ring
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    // Only check the outer ring (not interior)
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                        continue;

                    int checkX = startCell.x + dx;
                    int checkY = startCell.y + dy;

                    PathNode node = GetNodeAt(checkX, checkY);
                    if (node != null && node.isWalkable)
                    {
                        Debug.Log($"Found nearest walkable node at radius {radius}: {node.name}");
                        return node;
                    }
                }
            }
        }

        Debug.LogWarning($"No walkable node found within {maxSearchRadius} tiles of {worldPos}");
        return null;
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

        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                PathNode node = grid[x, y];

                if (node == null)
                    continue;

                node.neighbors.Clear();

                // Check all 4 directions
                if (x > 0 && grid[x - 1, y] != null)
                    node.neighbors.Add(grid[x - 1, y]);

                if (x < width - 1 && grid[x + 1, y] != null)
                    node.neighbors.Add(grid[x + 1, y]);

                if (y > 0 && grid[x, y - 1] != null)
                    node.neighbors.Add(grid[x, y - 1]);

                if (y < height - 1 && grid[x, y + 1] != null)
                    node.neighbors.Add(grid[x, y + 1]);
            }
        }
    }

    public PathNode GetNodeAt(int tilemapX, int tilemapY)
    {
        // Convert tilemap coordinates to grid array indices
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