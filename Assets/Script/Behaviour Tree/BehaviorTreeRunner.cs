using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BehaviorTreeRunner : MonoBehaviour
{
    private Node root;

    /// <summary>Per-hero private blackboard — local state only this hero reads/writes.</summary>
    protected Blackboard bb;

    /// <summary>
    /// Shared team blackboard — all heroes can read and write this.
    /// Null-safe: always check TeamBlackboard.Instance before using if you need
    /// the MonoBehaviour, or just use this reference for raw key access.
    /// </summary>
    protected Blackboard team => TeamBlackboard.Instance?.shared;

    [Header("Config - Override in child classes or Inspector")]
    public Transform target;  // e.g., player

    // ── Debug overlay ─────────────────────────────────────────────────────────
    // Press backtick (`) at runtime to toggle the on-screen BT decision panel.
    // All active runners register here so the first one can draw a single window
    // for the whole party — no extra manager GameObject needed.
    private static readonly List<BehaviorTreeRunner> _allRunners = new List<BehaviorTreeRunner>();
    private static bool _showDebug = false;

    protected virtual void Start()
    {
        bb = new Blackboard();
        bb.Set("self", transform);

        // Register on the team board so other heroes know where this one is
        TeamBlackboard.Instance?.Set("hero_" + gameObject.GetInstanceID(), transform);

        _allRunners.Add(this);

        root = BuildTree();
    }

    protected virtual void OnDestroy()
    {
        _allRunners.Remove(this);

        // Remove this hero's team-board entry when they die / are cleaned up
        TeamBlackboard.Instance?.Remove("hero_" + gameObject.GetInstanceID());

        // Clean up any hidden helper GameObjects owned by leaf nodes.
        // Node is a plain C# class so it can't hook into Unity's lifecycle directly.
        CleanupNodes(root);

        if (_btFallbackGO != null)
        {
            Destroy(_btFallbackGO);
            _btFallbackGO = null;
        }
    }

    /// <summary>Walks the tree and calls Cleanup() on any node that exposes it.</summary>
    private static void CleanupNodes(Node node)
    {
        if (node == null) return;
        if (node is FollowLeader fl) fl.Cleanup();
        foreach (var child in node.children)
            CleanupNodes(child);
    }

    protected virtual Node BuildTree()
    {
        // Override in subclasses or build dynamically
        return null;  // Placeholder
    }

    // BT tick rate — evaluate at most this many times per second.
    // Heroes don't need 60 fps AI; 20 Hz is more than responsive enough for
    // combat and exploration while cutting per-frame scanning work by 3×.
    private const float BtTickInterval = 0.05f;   // 20 ticks / sec
    private float _nextBtTick = 0f;

    // How many consecutive Failure ticks must occur before the fallback fires.
    // At 20 Hz, 4 failures ≈ 0.2 s — enough to bridge the gap when a target dies
    // and the next sequence hasn't started yet.
    private const int FailureStopThreshold = 4;
    private int _consecutiveFailures = 0;

    // Reusable GO used as the leader-path target when the BT hard-fails for a follower.
    private GameObject _btFallbackGO = null;

    // ── Global progress detector ──────────────────────────────────────────────
    // Watches whether the hero has made meaningful spatial progress during any
    // movement-oriented BT phase.  If not, forces a clean-slate reset so the BT
    // can re-evaluate from scratch rather than staying locked in a twitching loop.
    //
    // This catches cases that individual node stuck-checks miss, e.g. rapid-success
    // cycling where each iteration finishes before the per-node 1.5 s clock fires.
    //
    // Does NOT apply to "follow" or "wait upgrades" phases — those are intentionally
    // stationary states where the hero is supposed to stand near the leader/upgrade.
    private const float ProgressCheckInterval = 2.0f;   // check every 2 s
    private const float MinProgressDistance   = 0.8f;   // must have moved at least 0.8 u
    private float   _nextProgressCheck  = 0f;
    private Vector3 _progressCheckPos   = Vector3.zero;

    private void Update()
    {
        // Toggle debug overlay (checked every frame so it's responsive).
        if (Input.GetKeyDown(KeyCode.BackQuote))
            _showDebug = !_showDebug;

        if (root == null) return;
        if (Time.time < _nextBtTick) return;
        _nextBtTick = Time.time + BtTickInterval;

        NodeState result = root.Evaluate();

        // If every priority in the tree failed, apply a fallback:
        //
        //   FOLLOWERS — path straight to the leader at catch-up speed.
        //     FollowLeader normally prevents this (it always returns Running for
        //     a valid follower), but if something goes wrong (leader briefly null,
        //     FormationManager not ready, etc.) this hard-fallback keeps the hero
        //     from stopping indefinitely in a distant room.
        //
        //   LEADER / solo hero — stop any lingering movement so the hero doesn't
        //     drift toward a stale destination while the BT has nothing to drive it.
        if (result == NodeState.Failure)
        {
            bb?.Set("debugPhase", "⚠ Fallback");

            _consecutiveFailures++;
            if (_consecutiveFailures >= FailureStopThreshold)
            {
                _consecutiveFailures = 0;   // reset so we re-evaluate in FailureStopThreshold frames

                var fm     = FormationManager.Instance;
                bool isFollower = fm != null && !fm.IsLeader(transform);
                Transform leader = isFollower ? fm.GetLeader() : null;

                if (leader != null)
                {
                    // Follower with a live leader — path to them as hard fallback.
                    if (_btFallbackGO == null)
                        _btFallbackGO = new GameObject("_BtFallback")
                                        { hideFlags = HideFlags.HideAndDontSave };
                    _btFallbackGO.transform.position = leader.position;
                    GetComponent<MovementComponent>()?.OnTriggerMove(
                        transform, _btFallbackGO.transform, 1.45f);
                }
                else
                {
                    // Leader or no leader found — just stop stale movement.
                    GetComponent<UnitPathFollower>()?.StopPath();
                }
            }
        }
        else
        {
            _consecutiveFailures = 0;
        }

        // ── Global progress detector ──────────────────────────────────────────
        if (Time.time >= _nextProgressCheck && bb != null)
        {
            float moved        = Vector3.Distance(transform.position, _progressCheckPos);
            _progressCheckPos  = transform.position;
            _nextProgressCheck = Time.time + ProgressCheckInterval;

            string phase = bb.Get<string>("debugPhase") ?? "";
            if (moved < MinProgressDistance && IsStuckablePhase(phase))
            {
                // Hero is in a movement phase but has barely moved — likely stuck in a
                // rapid-cycle loop or a path that keeps failing silently.  Force a full
                // blackboard + sequence reset so the BT re-evaluates from P0 with fresh
                // state next tick.  The debug overlay will show "⚠ Unstuck" for one cycle.
                Debug.Log($"[BT] {gameObject.name} stuck in '{phase}' " +
                          $"(moved {moved:F2} u in {ProgressCheckInterval} s) — forcing reset");

                bb.Set<Transform>("target",     null);
                bb.Set<Transform>("itemTarget", null);
                bb.Set<Transform>("healTarget", null);
                bb.Set("debugPhase", "⚠ Unstuck");
                GetComponent<UnitPathFollower>()?.StopPath();
                ResetSequences(root);
            }
        }
    }

    // ── Stuck-detection helpers ───────────────────────────────────────────────

    /// <summary>
    /// Returns true for BT phases where the hero should be making spatial progress.
    /// "follow" and "wait upgrades" are intentionally low/no-movement — excluded.
    /// </summary>
    private static bool IsStuckablePhase(string phase)
    {
        if (string.IsNullOrEmpty(phase) || phase == "—") return false;
        string p = phase.ToLowerInvariant();
        return p.Contains("attack")  || p.Contains("loot")    || p.Contains("item")
            || p.Contains("explore") || p.Contains("extract") || p.Contains("heal")
            || p.Contains("guard")   || p.Contains("yield")   || p.Contains("fallback");
        // NOT "follow", "wait", "share", "flee", or "rally":
        //   flee  — hero may be stationary at safe spot; hysteresis handles exit.
        //   rally — leader holds intentionally; not a stuck scenario.
        // NOT "unstuck" — avoid immediately re-triggering on the reset cycle itself.
    }

    /// <summary>
    /// Recursively resets all <see cref="Sequence"/> nodes in the tree to their
    /// first child so the BT re-evaluates every branch from scratch next tick.
    /// </summary>
    private static void ResetSequences(Node node)
    {
        if (node == null) return;
        if (node is Sequence s) s.Reset();
        foreach (var child in node.children)
            ResetSequences(child);
    }

    // ── Debug overlay (OnGUI) ─────────────────────────────────────────────────
    // Only the first runner in the static list draws the window so we get exactly
    // one panel regardless of how many heroes are alive.
    //
    // Columns:
    //   Role     — ★ leader  · follower
    //   Class    — AI subclass with the "AI" suffix stripped
    //   Phase    — active LabeledSequence label  (e.g. "1: Attack")
    //   Target   — bb["target"] name + distance in world units
    //   HP       — current / max from HealthComponent
    private void OnGUI()
    {
        // Only the first runner draws UI so we get exactly one panel.
        if (_allRunners.Count == 0 || _allRunners[0] != this) return;

        const float panelX  = 10f;
        const float panelY  = 42f;   // offset below the top-left FPS / state text
        const float panelW  = 600f;
        const float rowH    = 22f;

        // ── Hint text when panel is hidden ────────────────────────────────────
        if (!_showDebug)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(new Rect(panelX, panelY, panelW, 20f),
                      "Press ` to show debug window");
            GUI.color = Color.white;
            return;
        }

        float panelH  = rowH * (_allRunners.Count + 2) + 12f;

        // Semi-transparent background
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(panelX + 6f, panelY + 6f, panelW - 12f, panelH - 12f));

        // Header row
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>BT Debug</b>  [ ` to hide ]", StyleHeader(), GUILayout.Width(200));
        GUILayout.Label("<b>Phase</b>",                   StyleHeader(), GUILayout.Width(160));
        GUILayout.Label("<b>Target</b>",                  StyleHeader(), GUILayout.Width(160));
        GUILayout.Label("<b>HP</b>",                      StyleHeader(), GUILayout.Width(70));
        GUILayout.EndHorizontal();

        // Divider
        GUILayout.Label("──────────────────────────────────────────────────────────────────");

        foreach (var runner in _allRunners)
        {
            if (runner == null) continue;

            bool   isLeader  = FormationManager.Instance?.IsLeader(runner.transform) == true;
            string role      = isLeader ? "★" : "·";
            string cls       = runner.GetType().Name.Replace("AI", "");
            string heroLabel = $"{role} {runner.gameObject.name} <i>({cls})</i>";

            string phase = runner.bb?.Get<string>("debugPhase") ?? "—";

            Transform tgt    = runner.bb?.Get<Transform>("target");
            string    tgtStr = "—";
            if (tgt != null)
            {
                float dist = Vector3.Distance(runner.transform.position, tgt.position);
                tgtStr = $"{tgt.name} ({dist:F1}u)";
            }

            var    hp    = runner.GetComponent<HealthComponent>();
            string hpStr = hp != null ? $"{hp.currentHealth:F0}/{hp.maxHealth:F0}" : "—";

            // Color the phase label by category
            Color phaseColor = PhaseColor(phase);
            GUIStyle phaseStyle = new GUIStyle(GUI.skin.label)
            {
                normal  = { textColor = phaseColor },
                richText = true,
            };

            GUILayout.BeginHorizontal();
            GUILayout.Label(heroLabel, StyleRow(), GUILayout.Width(200));
            GUILayout.Label(phase,     phaseStyle, GUILayout.Width(160));
            GUILayout.Label(tgtStr,    StyleRow(), GUILayout.Width(160));
            GUILayout.Label(hpStr,     StyleRow(), GUILayout.Width(70));
            GUILayout.EndHorizontal();
        }

        GUILayout.EndArea();
    }

    // ── GUI style helpers ─────────────────────────────────────────────────────

    private static GUIStyle StyleHeader()
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            normal   = { textColor = new Color(0.9f, 0.9f, 0.9f) },
        };
        return s;
    }

    private static GUIStyle StyleRow()
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            normal   = { textColor = new Color(0.85f, 0.85f, 0.85f) },
        };
        return s;
    }

    /// <summary>
    /// Returns a color keyed to the phase string so rows are scannable at a glance.
    ///   Red/orange  → combat (Attack, Heal Critical)
    ///   Green        → healing
    ///   Yellow       → looting / items
    ///   Cyan         → exploration / extraction
    ///   Blue         → following
    ///   Purple       → waiting for upgrades
    ///   Grey         → yielding space
    ///   White/amber  → fallback / unknown
    /// </summary>
    private static Color PhaseColor(string phase)
    {
        if (string.IsNullOrEmpty(phase) || phase == "—") return Color.grey;

        string p = phase.ToLowerInvariant();

        if (p.Contains("extract"))                    return new Color(0.0f, 1.0f, 1.0f);  // cyan
        if (p.Contains("flee"))                      return new Color(1.0f, 0.2f, 0.8f);  // magenta — panic
        if (p.Contains("rally"))                     return new Color(0.0f, 0.8f, 0.5f);  // sea-green — leader holding position
        if (p.Contains("attack"))                    return new Color(1.0f, 0.3f, 0.3f);  // red
        if (p.Contains("heal crit"))                 return new Color(1.0f, 0.5f, 0.1f);  // orange
        if (p.Contains("heal"))                      return new Color(0.3f, 1.0f, 0.4f);  // green
        if (p.Contains("share"))                     return new Color(0.4f, 1.0f, 0.7f);  // teal-green — generosity
        if (p.Contains("loot") || p.Contains("item")) return new Color(1.0f, 0.9f, 0.2f); // yellow
        if (p.Contains("yield"))                     return new Color(0.6f, 0.6f, 0.6f);  // grey
        if (p.Contains("follow"))                    return new Color(0.4f, 0.7f, 1.0f);  // blue
        if (p.Contains("wait") || p.Contains("upgrade")) return new Color(0.8f, 0.4f, 1.0f); // purple
        if (p.Contains("wary"))                       return new Color(1.0f, 0.65f, 0.0f); // amber — cautious diversion
        if (p.Contains("explore"))                   return new Color(0.2f, 0.9f, 0.8f);  // teal
        if (p.Contains("guard") || p.Contains("relic")) return new Color(1.0f, 0.7f, 0.2f); // amber
        if (p.Contains("fallback") || p.Contains("⚠")) return new Color(1.0f, 0.4f, 0.0f); // orange-red

        return new Color(0.85f, 0.85f, 0.85f);  // default light-grey
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────
    // Draws the hero's current movement target every frame in the Scene view.
    // Color encodes what the hero is doing:
    //   Red    → chasing an enemy
    //   Orange → moving to a chest
    //   Yellow → picking up a world item
    //   Green  → moving to heal an ally
    //   Cyan   → exploring fog (leader)
    //   Blue   → following the leader (follower)
    //   White  → unknown / fallback target
    //
    // Leader targets are drawn at full opacity with a larger sphere; follower
    // targets are drawn at half opacity so the leader stays visually dominant.
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || bb == null) return;

        Transform movTarget = bb.Get<Transform>("target");
        if (movTarget == null) return;

        bool   isLeader  = FormationManager.Instance?.IsLeader(transform) == true;
        Color  baseColor = ClassifyTarget(movTarget);
        baseColor.a      = isLeader ? 1.0f : 0.45f;
        Gizmos.color     = baseColor;

        // Line from hero to target destination.
        Gizmos.DrawLine(transform.position, movTarget.position);

        // Sphere at destination — larger for the leader so it's easy to spot.
        float radius = isLeader ? 0.35f : 0.2f;
        Gizmos.DrawWireSphere(movTarget.position, radius);

        // Solid dot at the hero's feet for a clear origin point.
        Gizmos.DrawSphere(transform.position, 0.12f);

#if UNITY_EDITOR
        // Text label at the destination — editor only, no runtime cost.
        string prefix = isLeader ? "★ " : "  ";
        string label  = prefix + DescribeTarget(movTarget);
        GUIStyle style = new GUIStyle
        {
            normal  = { textColor = baseColor },
            fontSize = isLeader ? 11 : 9
        };
        Handles.Label(movTarget.position + Vector3.up * 0.5f, label, style);
#endif
    }

    /// <summary>Returns a display color for the given movement target.</summary>
    private Color ClassifyTarget(Transform t)
    {
        if (t == null) return Color.white;

        // Enemy → red
        if (t.CompareTag("Enemy"))    return Color.red;

        // Chest → orange
        if (t.CompareTag("Lootable")) return new Color(1f, 0.55f, 0f);

        // World item — the hero may set "itemTarget" separately; check both.
        Transform itemTarget = bb?.Get<Transform>("itemTarget");
        if ((itemTarget != null && t == itemTarget) || t.GetComponent<WorldItem>() != null)
            return Color.yellow;

        // Heal target → green
        Transform healTarget = bb?.Get<Transform>("healTarget");
        if (healTarget != null && t == healTarget) return Color.green;

        // Distinguish explore (leader) from follow (follower) by role.
        bool isLeader = FormationManager.Instance?.IsLeader(transform) == true;
        return isLeader
            ? new Color(0.0f, 1.0f, 1.0f)   // cyan  — leader exploring fog
            : new Color(0.3f, 0.6f, 1.0f);  // blue  — follower chasing leader
    }

#if UNITY_EDITOR
    /// <summary>Short human-readable description of what the target is.</summary>
    private string DescribeTarget(Transform t)
    {
        if (t == null) return "?";

        if (t.CompareTag("Enemy"))
        {
            var hp = t.GetComponent<HealthComponent>();
            return hp != null
                ? $"{t.name} ({hp.currentHealth:F0}/{hp.maxHealth:F0} HP)"
                : t.name;
        }

        if (t.CompareTag("Lootable"))    return $"Chest: {t.name}";

        var wi = t.GetComponent<WorldItem>();
        if (wi != null) return $"Item: {wi.item?.itemName ?? t.name}";

        Transform healTarget = bb?.Get<Transform>("healTarget");
        if (healTarget != null && t == healTarget) return $"Heal: {t.name}";

        bool isLeader = FormationManager.Instance?.IsLeader(transform) == true;
        return isLeader ? $"Explore → {t.name}" : $"Follow → {t.name}";
    }
#endif
}
