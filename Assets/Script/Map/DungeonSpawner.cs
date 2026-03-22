using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns the hero, enemies, and chests after the dungeon grid is ready.
/// Listens to GridGenerator.OnGridGenerated so pathfinding is usable before anything spawns.
///
/// On regeneration (called by RoundState_Lose):
///   CleanupAll() destroys everything, then DungeonGenerator.Regenerate() + GridGenerator.RegenerateGrid()
///   fire OnGridGenerated again which triggers a fresh spawn.
/// </summary>
public class DungeonSpawner : MonoBehaviour
{
    public static DungeonSpawner Instance { get; private set; }

    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("Prefabs")]
    [Tooltip("One or more enemy prefabs — a random one is picked per spawn.")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [Tooltip("Chest prefab with a Lootable component.")]
    [SerializeField] private GameObject chestPrefab;
    [Tooltip("The hero prefab. Instantiated once; repositioned on subsequent rounds.")]
    [SerializeField] private GameObject heroPrefab;
    [Tooltip("WorldItem prefab — injected into chests at runtime.")]
    [SerializeField] private GameObject worldItemPrefab;

    [Header("Loot")]
    [Tooltip("Default loot table assigned to every chest.")]
    [SerializeField] private LootTable defaultLootTable;
    [Tooltip("This item is guaranteed to drop from exactly one chest per dungeon.")]
    [SerializeField] private ItemSO relicItem;

    [Header("Spawn Counts")]
    [SerializeField] private int chestsPerMap    = 4;
    [SerializeField] private int minEnemiesPerRoom = 1;
    [SerializeField] private int maxEnemiesPerRoom = 3;

    // ─── Runtime State ─────────────────────────────────────────────────────

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private GameObject _hero;

    // ─── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()  => GridGenerator.OnGridGenerated += OnGridReady;
    private void OnDisable() => GridGenerator.OnGridGenerated -= OnGridReady;

    // ─── Spawn Entry Point ─────────────────────────────────────────────────

    private void OnGridReady()
    {
        // GridGenerator already waited for end-of-frame before firing this event,
        // so all nodes are fully linked and A* is ready.
        SpawnAll();
    }

    private void SpawnAll()
    {
        var gen = DungeonGenerator.Instance;
        if (gen == null || gen.Rooms.Count == 0)
        {
            Debug.LogWarning("[DungeonSpawner] No rooms to spawn into.");
            return;
        }

        int roomCount = gen.Rooms.Count;

        // ── Extraction point — always in room 0 (the start room) ──
        if (ExtractionPoint.Instance != null)
            ExtractionPoint.Instance.SetPosition(gen.GetRoomWorldCenter(0));

        // ── Hero ──  Room 0 is always the start room, no enemies here.
        PlaceHero(gen.GetRoomWorldCenter(0));

        // ── Pick rooms for chests (not start room) ──
        List<int> chestRooms = ShuffledRange(1, roomCount - 1, Mathf.Min(chestsPerMap, roomCount - 1));

        // ── Enemies in non-start, non-chest rooms ──
        for (int i = 1; i < roomCount; i++)
        {
            if (chestRooms.Contains(i)) continue;
            int count = Random.Range(minEnemiesPerRoom, maxEnemiesPerRoom + 1);
            for (int e = 0; e < count; e++)
                SpawnEnemy(gen.GetRandomPositionInRoom(i));
        }

        // ── Chests — one is guaranteed to hold the relic ──
        bool relicPlaced = false;
        foreach (int roomIdx in chestRooms)
        {
            bool makeRelicChest = !relicPlaced;
            SpawnChest(gen.GetRoomWorldCenter(roomIdx), makeRelicChest);
            if (makeRelicChest) relicPlaced = true;
        }

        Debug.Log($"[DungeonSpawner] Spawned hero, {_spawned.FindAll(o => o != null && o.CompareTag("Enemy")).Count} enemies, {chestRooms.Count} chests (relic placed: {relicPlaced}).");
    }

    // ─── Individual Spawners ───────────────────────────────────────────────

    private void PlaceHero(Vector3 pos)
    {
        if (_hero != null)
        {
            // Hero survived a round (shouldn't normally happen since they die on lose)
            // Just reposition them.
            _hero.transform.position = pos;
            return;
        }

        if (heroPrefab == null)
        {
            Debug.LogWarning("[DungeonSpawner] No heroPrefab assigned!");
            return;
        }

        _hero = Instantiate(heroPrefab, pos, Quaternion.identity);
        // Hero is NOT added to _spawned — it's managed separately.
    }

    private void SpawnEnemy(Vector3 pos)
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;
        var prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        if (prefab == null) return;

        var obj = Instantiate(prefab, pos, Quaternion.identity);
        _spawned.Add(obj);
    }

    private void SpawnChest(Vector3 pos, bool withRelic)
    {
        if (chestPrefab == null) return;

        var obj = Instantiate(chestPrefab, pos, Quaternion.identity);
        _spawned.Add(obj);

        var lootable = obj.GetComponent<Lootable>();
        if (lootable == null) return;

        if (defaultLootTable != null) lootable.SetLootTable(defaultLootTable);
        if (worldItemPrefab  != null) lootable.SetWorldItemPrefab(worldItemPrefab);
        if (withRelic && relicItem != null) lootable.SetGuaranteedItem(relicItem);
    }

    // ─── Cleanup (called before regeneration) ─────────────────────────────

    /// <summary>
    /// Destroys all spawned enemies, chests, world items, and the hero.
    /// Call this before DungeonGenerator.Regenerate().
    /// </summary>
    public void CleanupAll()
    {
        foreach (var obj in _spawned)
            if (obj != null) Destroy(obj);
        _spawned.Clear();

        // Destroy all world items lying on the ground
        // ToArray() because Unregister modifies the list during destroy
        var items = new List<WorldItem>(WorldItemRegistry.All);
        foreach (var wi in items)
            if (wi != null) Destroy(wi.gameObject);

        // Destroy hero — a fresh one is spawned from prefab next round
        if (_hero != null)
        {
            Destroy(_hero);
            _hero = null;
        }

        Debug.Log("[DungeonSpawner] Cleanup complete.");
    }

    /// <summary>
    /// Destroys enemies, chests, and world items but keeps the hero alive with
    /// their current equipment. Called by RoundState_Win so the hero progresses
    /// to the next floor without losing their gear.
    /// Also resets the hero's RelicHolder so they start the next floor without the Relic.
    /// </summary>
    public void CleanupExceptHero()
    {
        foreach (var obj in _spawned)
            if (obj != null) Destroy(obj);
        _spawned.Clear();

        // Destroy all world items lying on the ground
        var items = new List<WorldItem>(WorldItemRegistry.All);
        foreach (var wi in items)
            if (wi != null) Destroy(wi.gameObject);

        // Reset the hero's relic possession — the relic stays in the old dungeon
        if (_hero != null)
        {
            RelicHolder holder = _hero.GetComponent<RelicHolder>();
            holder?.Reset();
        }

        Debug.Log("[DungeonSpawner] Cleanup (except hero) complete.");
    }

    // ─── Utility ──────────────────────────────────────────────────────────

    /// <summary>Returns a shuffled list of 'count' indices in [min, max] inclusive.</summary>
    private List<int> ShuffledRange(int min, int max, int count)
    {
        var pool = new List<int>();
        for (int i = min; i <= max; i++) pool.Add(i);

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        count = Mathf.Clamp(count, 0, pool.Count);
        return pool.GetRange(0, count);
    }
}
