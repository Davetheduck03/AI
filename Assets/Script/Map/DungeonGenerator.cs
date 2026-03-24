using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Procedural dungeon generator using Binary Space Partitioning (BSP).
/// Paints floor and wall tiles onto the tilemaps, then fires OnDungeonGenerated
/// so that GridGenerator can rebuild its pathfinding nodes from the new layout.
///
/// Execution order -100 ensures this runs in Start() BEFORE GridGenerator
/// so tiles are painted before nodes are created.
/// </summary>
[DefaultExecutionOrder(-100)]
public class DungeonGenerator : MonoBehaviour
{
    public static DungeonGenerator Instance { get; private set; }

    /// <summary>Fired after tiles have been painted. GridGenerator and DungeonSpawner listen to this.</summary>
    public static event Action OnDungeonGenerated;

    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("Tilemaps")]
    [Tooltip("The walkable floor tilemap — also used by GridGenerator for pathfinding nodes.")]
    [SerializeField] private Tilemap floorTilemap;
    [Tooltip("The wall tilemap painted around the dungeon perimeter.")]
    [SerializeField] private Tilemap wallTilemap;
    [Tooltip("Tile asset painted on the floor tilemap.")]
    [SerializeField] private TileBase floorTile;
    [Tooltip("Tile asset painted on the wall tilemap.")]
    [SerializeField] private TileBase wallTile;

    [Header("Map Size (in tiles)")]
    [SerializeField] private int mapWidth  = 60;
    [SerializeField] private int mapHeight = 60;

    [Header("Room Settings")]
    [SerializeField] private int minRoomSize = 6;
    [SerializeField] private int maxRoomSize = 14;
    [Tooltip("BSP split depth — more depth means more, smaller rooms.")]
    [SerializeField] private int bspDepth = 4;

    // ─── State ─────────────────────────────────────────────────────────────

    private bool[,]          _floorMap;
    private readonly List<RectInt> _rooms = new List<RectInt>();

    /// <summary>All carved rooms in tilemap cell coordinates (populated after Generate).</summary>
    public IReadOnlyList<RectInt> Rooms => _rooms;

    // ─── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Generation is triggered by RoundState_PartySelect.Confirm() → Regenerate(),
    // not at startup — the dungeon only needs to exist during active gameplay.

    // ─── Public API ────────────────────────────────────────────────────────

    /// <summary>Clears the tilemaps and generates a brand-new dungeon.</summary>
    public void Regenerate()
    {
        Generate(); // Generate() now clears tiles itself
    }

    /// <summary>World-space centre of the given room index.</summary>
    public Vector3 GetRoomWorldCenter(int roomIndex)
    {
        if (roomIndex < 0 || roomIndex >= _rooms.Count) return Vector3.zero;
        RectInt r = _rooms[roomIndex];
        return floorTilemap.GetCellCenterWorld(
            new Vector3Int(r.x + r.width / 2, r.y + r.height / 2, 0));
    }

    /// <summary>Random walkable world-space point inside the given room (1-tile inset from edges).</summary>
    public Vector3 GetRandomPositionInRoom(int roomIndex)
    {
        if (roomIndex < 0 || roomIndex >= _rooms.Count) return Vector3.zero;
        RectInt r = _rooms[roomIndex];
        int cx = UnityEngine.Random.Range(r.x + 1, r.x + r.width  - 1);
        int cy = UnityEngine.Random.Range(r.y + 1, r.y + r.height - 1);
        return floorTilemap.GetCellCenterWorld(new Vector3Int(cx, cy, 0));
    }

    // ─── Core Generation ───────────────────────────────────────────────────

    private void Generate()
    {
        // Always start with a clean slate so leftover tiles never mix with the new layout
        floorTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();

        _rooms.Clear();
        _floorMap = new bool[mapWidth, mapHeight];

        BSPNode root = new BSPNode(new RectInt(0, 0, mapWidth, mapHeight));
        SplitNode(root, bspDepth);
        CreateRooms(root);
        ConnectRooms(root);
        PaintTiles();

        OnDungeonGenerated?.Invoke();
        Debug.Log($"[DungeonGenerator] Generated {_rooms.Count} rooms.");
    }

    // ─── BSP ───────────────────────────────────────────────────────────────

    private class BSPNode
    {
        public RectInt area;
        public BSPNode left, right;
        public RectInt room;
        public bool    hasRoom;

        public BSPNode(RectInt area) { this.area = area; }

        public Vector2Int RoomCenter =>
            new Vector2Int(room.x + room.width / 2, room.y + room.height / 2);
    }

    private void SplitNode(BSPNode node, int depth)
    {
        if (depth == 0) return;
        if (TrySplit(node))
        {
            SplitNode(node.left,  depth - 1);
            SplitNode(node.right, depth - 1);
        }
    }

    private bool TrySplit(BSPNode node)
    {
        // Prefer splitting along the longer axis; fall back to random
        int minPartition = minRoomSize + 4;   // room + padding on each side
        bool splitHorizontal =
            node.area.height > node.area.width * 1.25f ? true  :
            node.area.width  > node.area.height * 1.25f ? false :
            UnityEngine.Random.value > 0.5f;

        if (splitHorizontal)
        {
            if (node.area.height < minPartition * 2) return false;
            int split = UnityEngine.Random.Range(minPartition, node.area.height - minPartition);
            node.left  = new BSPNode(new RectInt(node.area.x, node.area.y,
                                                  node.area.width, split));
            node.right = new BSPNode(new RectInt(node.area.x, node.area.y + split,
                                                  node.area.width, node.area.height - split));
        }
        else
        {
            if (node.area.width < minPartition * 2) return false;
            int split = UnityEngine.Random.Range(minPartition, node.area.width - minPartition);
            node.left  = new BSPNode(new RectInt(node.area.x, node.area.y,
                                                  split, node.area.height));
            node.right = new BSPNode(new RectInt(node.area.x + split, node.area.y,
                                                  node.area.width - split, node.area.height));
        }
        return true;
    }

    private void CreateRooms(BSPNode node)
    {
        // Leaf node — carve a room inside the partition
        if (node.left == null && node.right == null)
        {
            int maxW = Mathf.Min(maxRoomSize, node.area.width  - 2);
            int maxH = Mathf.Min(maxRoomSize, node.area.height - 2);

            if (maxW < minRoomSize || maxH < minRoomSize) return;  // partition too small

            int w = UnityEngine.Random.Range(minRoomSize, maxW + 1);
            int h = UnityEngine.Random.Range(minRoomSize, maxH + 1);
            int x = node.area.x + UnityEngine.Random.Range(1, node.area.width  - w);
            int y = node.area.y + UnityEngine.Random.Range(1, node.area.height - h);

            node.room    = new RectInt(x, y, w, h);
            node.hasRoom = true;

            for (int fx = x; fx < x + w; fx++)
                for (int fy = y; fy < y + h; fy++)
                    SetFloor(fx, fy);

            _rooms.Add(node.room);
            return;
        }

        if (node.left  != null) CreateRooms(node.left);
        if (node.right != null) CreateRooms(node.right);
    }

    private void ConnectRooms(BSPNode node)
    {
        if (node.left == null || node.right == null) return;

        ConnectRooms(node.left);
        ConnectRooms(node.right);

        Vector2Int a = GetLeafCenter(node.left);
        Vector2Int b = GetLeafCenter(node.right);
        CarveCorridorLShaped(a, b);
    }

    // Walk down to a leaf node that has a room and return its centre
    private Vector2Int GetLeafCenter(BSPNode node)
    {
        if (node.hasRoom) return node.RoomCenter;
        if (node.left  != null) { var c = GetLeafCenter(node.left);  if (c != Vector2Int.zero) return c; }
        if (node.right != null) { var c = GetLeafCenter(node.right); if (c != Vector2Int.zero) return c; }
        return Vector2Int.zero;
    }

    private void CarveCorridorLShaped(Vector2Int a, Vector2Int b)
    {
        // Randomly choose whether to go horizontal-first or vertical-first
        Vector2Int elbow = UnityEngine.Random.value > 0.5f
            ? new Vector2Int(b.x, a.y)
            : new Vector2Int(a.x, b.y);

        CarveLine(a, elbow);
        CarveLine(elbow, b);
    }

    private void CarveLine(Vector2Int from, Vector2Int to)
    {
        int x = from.x, y = from.y;

        // Horizontal segment — widen by 1 tile above and below
        while (x != to.x)
        {
            SetFloor(x, y);
            SetFloor(x, y - 1);
            SetFloor(x, y + 1);
            x += (x < to.x) ? 1 : -1;
        }

        // Vertical segment — widen by 1 tile left and right
        while (y != to.y)
        {
            SetFloor(x, y);
            SetFloor(x - 1, y);
            SetFloor(x + 1, y);
            y += (y < to.y) ? 1 : -1;
        }

        // End point + cross so the elbow junction is fully open
        SetFloor(to.x,     to.y);
        SetFloor(to.x - 1, to.y);
        SetFloor(to.x + 1, to.y);
        SetFloor(to.x,     to.y - 1);
        SetFloor(to.x,     to.y + 1);
    }

    private void SetFloor(int x, int y)
    {
        if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight) return;
        _floorMap[x, y] = true;
    }

    // ─── Tile Painting ─────────────────────────────────────────────────────

    private void PaintTiles()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                var cell = new Vector3Int(x, y, 0);

                if (_floorMap[x, y])
                {
                    floorTilemap.SetTile(cell, floorTile);
                }
                else if (wallTilemap != null && wallTile != null && IsAdjacentToFloor(x, y))
                {
                    wallTilemap.SetTile(cell, wallTile);
                }
            }
        }
    }

    private bool IsAdjacentToFloor(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < mapWidth && ny < mapHeight && _floorMap[nx, ny])
                    return true;
            }
        return false;
    }
}
