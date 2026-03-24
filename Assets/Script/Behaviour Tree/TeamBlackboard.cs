using UnityEngine;

/// <summary>
/// A shared blackboard visible to every hero's behaviour tree.
/// Use it for team-level coordination: who holds the relic, item offers,
/// threat awareness, support requests, etc.
///
/// Place this component on a persistent scene GameObject (e.g. "GameManager"
/// or a dedicated "TeamAI" object). Heroes read/write it via the static Instance.
///
/// Common keys (by convention — not enforced):
///   "relicHolder"     Transform   — the hero currently carrying the relic (null if none)
///   "offeredItem"     WorldItem   — a world item flagged for a specific ally to collect
///   "offeredItemFor"  Transform   — the hero the offer is intended for
/// </summary>
public class TeamBlackboard : MonoBehaviour
{
    public static TeamBlackboard Instance { get; private set; }

    // Exposed as internal so BehaviorTreeRunner can hand the raw Blackboard
    // to nodes that need both the per-hero board and the team board.
    internal readonly Blackboard shared = new Blackboard();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API — mirrors Blackboard so callers don't need to know internals ──

    public void Set<T>(string key, T value)  => shared.Set(key, value);
    public T    Get<T>(string key)           => shared.Get<T>(key);
    public bool Has(string key)              => shared.Has(key);
    public void Remove(string key)           => shared.Remove(key);

    // ── Convenience helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Registers the hero that just picked up the relic so every other hero
    /// knows who to protect or follow toward the extraction point.
    /// </summary>
    public void SetRelicHolder(Transform hero) => Set("relicHolder", hero);
    public Transform GetRelicHolder()          => Get<Transform>("relicHolder");
    public void ClearRelicHolder()             => Remove("relicHolder");

    /// <summary>
    /// Posts an item offer so a specific ally can come claim it instead of
    /// the nearest hero walking over it.
    /// </summary>
    public void OfferItem(WorldItem item, Transform intendedRecipient)
    {
        Set("offeredItem",    item);
        Set("offeredItemFor", intendedRecipient);
    }

    public WorldItem GetOfferedItem()         => Get<WorldItem>("offeredItem");
    public Transform GetOfferedItemRecipient()=> Get<Transform>("offeredItemFor");

    public void ClearItemOffer()
    {
        Remove("offeredItem");
        Remove("offeredItemFor");
    }
}
