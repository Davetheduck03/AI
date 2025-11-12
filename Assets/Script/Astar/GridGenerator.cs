using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridGenerator : MonoBehaviour
{
    public static event Action OnGridGenerated;
    public static GridGenerator Instance;

    [Header("Grid Settings")]
    public int width = 1;
    public int height = 1;
    public float cellSize = 1f;

    [Header("Tilemap Settings")]
    public Tilemap walkableTilemap;

    [Header("Node Settings")]
    public GameObject nodePrefab;

    private PathNode[,] grid;

    private void Start()
    {
        GridGenerator.Instance = this;
        GenerateGrid();
        LinkNeighbors();
        StartCoroutine(WaitTillEndOfFrame());
    }

    public void GenerateGrid()
    {
        // Ensure grid array exists and matches current dimensions
        if (grid == null || grid.GetLength(0) != width || grid.GetLength(1) != height)
            grid = new PathNode[width, height];

        Vector3 origin = transform.position;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Check if this position has a walkable tile
                if (!IsTileWalkable(x, y))
                {
                    grid[x, y] = null; // Mark as non-walkable
                    continue;
                }

                // Reuse existing node if already present
                if (grid[x, y] != null)
                {
                    PathNode existingNode = grid[x, y];
                    existingNode.isWalkable = true;
                    existingNode.gridPosition = new Vector2Int(x, y);
                    continue; // Skip instantiation
                }

                // Spawn new node only for walkable tiles
                Vector3 worldPos = origin + new Vector3(
                    x * cellSize + cellSize / 2f,
                    y * cellSize + cellSize / 2f,
                    0f);

                GameObject nodeObj = Instantiate(nodePrefab, worldPos, Quaternion.identity, transform);
                nodeObj.name = $"Node ({x},{y})";
                PathNode node = nodeObj.GetComponent<PathNode>();
                node.gridPosition = new Vector2Int(x, y);
                node.isWalkable = true;
                grid[x, y] = node;
                Astar.Instance.allNodes.Add(node);
            }
        }
    }

    private bool IsTileWalkable(int gridX, int gridY)
    {
        if (walkableTilemap == null)
        {
            Debug.LogWarning("Walkable Tilemap is not assigned!");
            return true; // Fallback to spawning all nodes
        }

        // Convert grid position to world position (center of cell)
        Vector3 origin = transform.position;
        Vector3 worldPos = origin + new Vector3(
            gridX * cellSize + cellSize / 2f,
            gridY * cellSize + cellSize / 2f,
            0f);

        // Convert world position to cell position in tilemap
        Vector3Int cellPos = walkableTilemap.WorldToCell(worldPos);

        // Check if there's a tile at this cell position
        TileBase tile = walkableTilemap.GetTile(cellPos);
        return tile != null;
    }

    private IEnumerator WaitTillEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        OnGridGenerated?.Invoke();
    }

    private void LinkNeighbors()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                PathNode node = grid[x, y];

                if (node == null)
                    continue;

                node.neighbors.Clear();

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

    public PathNode GetNodeAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
            return null;
        return grid[x, y];
    }
}