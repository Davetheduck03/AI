using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns the party, enemies, and chests after the dungeon grid is ready.
/// Listens to GridGenerator.OnGridGenerated so pathfinding is usable before anything spawns.
///
/// Party heroes come from PartyData.Instance.SelectedParty — the list built in the
/// party selection screen. Up to 4 heroes are placed in the start room (room 0),
/// spread around the room centre so they don't overlap.
///
/// Death tracking: BaseHero calls OnHeroDied() when a hero dies.
/// Only when every hero is dead does this trigger RoundState_Lose.
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
    [Tooltip("WorldItem prefab — injected into chests at runtime.")]
    [SerializeField] private GameObject worldItemPrefab;

    [Header("Loot")]
    [Tooltip("Default loot table assigned to every chest.")]
    [SerializeField] private LootTable defaultLootTable;
    [Tooltip("This item is guaranteed to drop from exactly one chest per dungeon.")]
    [SerializeField] private ItemSO relicItem;

    [Header("Spawn Counts")]
    [SerializeField] private int chestsPerMap     = 4;
    [SerializeField] private int minEnemiesPerRoom = 1;
    [SerializeField] private int maxEnemiesPerRoom = 3;

    // ─── Runtime State ─────────────────────────────────────────────────────

    private readonly List<GameObject> _spawned = new List<GameObject>();

    // All living hero GameObjects — populated in PlaceParty, pruned in OnHeroDied.
    private readonly List<GameObject> _heroes = new List<GameObject>();

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

        // Only spawn when the game is actively in the InGame state.
        // The grid also generates at startup (before party selection), so we must
        // ignore that initial event and wait for the one triggered by Confirm().
        if (!(GameManager.Instance?.GetCurrentState() is RoundState_InGame))
            return;

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

        // ── Party — spawned in room 0 if not already alive (Win flow keeps them) ──
        if (_heroes.Count == 0)
            PlaceParty(gen.GetRoomWorldCenter(0));
        else
            RepositionParty(gen.GetRoomWorldCenter(0));

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

        Debug.Log($"[DungeonSpawner] Spawned {_heroes.Count} hero(es), " +
                  $"{_spawned.FindAll(o => o != null && o.CompareTag("Enemy")).Count} enemies, " +
                  $"{chestRooms.Count} chests (relic placed: {relicPlaced}).");
    }

    // ─── Party Spawning ────────────────────────────────────────────────────

    /// <summary>Fresh spawn of the selected party at the start of a new run.</summary>
    private void PlaceParty(Vector3 centerPos)
    {
        var party = PartyData.Instance?.SelectedParty;
        if (party == null || party.Count == 0)
        {
            Debug.LogWarning("[DungeonSpawner] No party selected — nothing spawned.");
            return;
        }

        for (int i = 0; i < party.Count; i++)
        {
            var entry = party[i];
            if (entry?.prefab == null)
            {
                Debug.LogWarning($"[DungeonSpawner] Party entry {i} has no prefab assigned.");
                continue;
            }

            Vector3 pos = centerPos + PartySpawnOffset(i, party.Count);
            GameObject hero = Instantiate(entry.prefab, pos, Quaternion.identity);
            _heroes.Add(hero);
        }
    }

    /// <summary>
    /// On a floor transition (Win) the heroes are kept alive but the dungeon is new.
    /// Just move them back to the new start room centre.
    /// </summary>
    private void RepositionParty(Vector3 centerPos)
    {
        for (int i = 0; i < _heroes.Count; i++)
        {
            if (_heroes[i] == null) continue;
            _heroes[i].transform.position = centerPos + PartySpawnOffset(i, _heroes.Count);
        }
    }

    /// <summary>
    /// Spreads N heroes in a small circle so they don't stack on top of each other.
    /// A single hero gets no offset (placed exactly at the room centre).
    /// </summary>
    private static Vector3 PartySpawnOffset(int index, int total)
    {
        if (total <= 1) return Vector3.zero;
        float angle = (360f / total) * index * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 1.5f;
    }

    // ─── Death Tracking ────────────────────────────────────────────────────

    /// <summary>
    /// Called by BaseHero when a hero's HealthComponent fires OnDeath.
    /// Removes the hero from the living list; triggers Lose if all heroes are gone.
    /// </summary>
    public void OnHeroDied(BaseHero hero)
    {
        _heroes.Remove(hero.gameObject);

        Debug.Log($"[DungeonSpawner] {hero.name} died. Living heroes: {_heroes.Count}");

        if (_heroes.Count <= 0)
        {
            Debug.Log("[DungeonSpawner] All heroes dead — triggering Lose.");
            GameManager.Instance?.SwitchState(GameManager.Instance.Lose);
        }
    }

    // ─── Individual Spawners ───────────────────────────────────────────────

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

        // Relic chest only ever drops the relic — no random equipment on top of it.
        if (!withRelic && defaultLootTable != null) lootable.SetLootTable(defaultLootTable);
        if (worldItemPrefab != null) lootable.SetWorldItemPrefab(worldItemPrefab);
        if (withRelic && relicItem != null) lootable.SetGuaranteedItem(relicItem);
    }

    // ─── Cleanup ──────────────────────────────────────────────────────────

    /// <summary>
    /// Destroys everything including all heroes. Called on Lose before going back
    /// to the party selection screen.
    /// </summary>
    public void CleanupAll()
    {
        foreach (var obj in _spawned)
            if (obj != null) Destroy(obj);
        _spawned.Clear();

        var items = new List<WorldItem>(WorldItemRegistry.All);
        foreach (var wi in items)
            if (wi != null) Destroy(wi.gameObject);

        foreach (var hero in _heroes)
            if (hero != null) Destroy(hero);
        _heroes.Clear();

        Debug.Log("[DungeonSpawner] Full cleanup complete.");
    }

    /// <summary>
    /// Destroys enemies, chests, and world items but keeps surviving heroes alive
    /// with their current equipment. Called by RoundState_Win before the next floor.
    /// Resets every hero's RelicHolder so the relic must be found again.
    /// </summary>
    public void CleanupExceptHeroes()
    {
        foreach (var obj in _spawned)
            if (obj != null) Destroy(obj);
        _spawned.Clear();

        var items = new List<WorldItem>(WorldItemRegistry.All);
        foreach (var wi in items)
            if (wi != null) Destroy(wi.gameObject);

        foreach (var hero in _heroes)
        {
            if (hero == null) continue;
            hero.GetComponent<RelicHolder>()?.Reset();
        }

        Debug.Log($"[DungeonSpawner] Cleanup (except {_heroes.Count} hero(es)) complete.");
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
