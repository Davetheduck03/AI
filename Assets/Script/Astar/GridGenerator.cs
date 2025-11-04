using System;
using System.Collections;
using UnityEngine;

namespace TowerDefenseTK
{

    public class GridGenerator : MonoBehaviour
    {
        public static event Action OnGridGenerated;

        public static GridGenerator Instance;

        [Header("Grid Settings")]
        public int width = 1;
        public int height = 1;
        public float cellSize = 1f;

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
                    // Reuse existing node if already present
                    if (grid[x, y] != null)
                    {
                        PathNode existingNode = grid[x, y];  // <-- Different name
                        existingNode.isWalkable = true;
                        existingNode.gridPosition = new Vector2Int(x, y);
                        continue; // Skip instantiation
                    }

                    // Spawn new node
                    Vector3 worldPos = origin + new Vector3(
                        x * cellSize + cellSize / 2f,
                        y * cellSize + cellSize / 2f,
                        0f);

                    GameObject nodeObj = Instantiate(nodePrefab, worldPos, Quaternion.identity, transform);
                    nodeObj.name = $"Node ({x},{y})";

                    PathNode node = nodeObj.GetComponent<PathNode>(); // <-- 'node' only declared once here
                    node.gridPosition = new Vector2Int(x, y);
                    node.isWalkable = true;

                    grid[x, y] = node;
                    Astar.Instance.allNodes.Add(node);
                }
            }
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
                    node.neighbors.Clear();

                    if (x > 0) node.neighbors.Add(grid[x - 1, y]);
                    if (x < width - 1) node.neighbors.Add(grid[x + 1, y]);
                    if (y > 0) node.neighbors.Add(grid[x, y - 1]);
                    if (y < height - 1) node.neighbors.Add(grid[x, y + 1]);
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
}
