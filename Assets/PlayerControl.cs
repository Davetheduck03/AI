using UnityEngine;

/// <summary>
/// Allows the player to manually move the hero currently targeted by CameraController
/// using WASD / arrow keys.
///
/// HOW IT WORKS:
///   Each frame, this script reads CameraController.CurrentHero to find whichever
///   hero the camera is locked onto.  WASD input moves that hero's Transform directly,
///   bypassing the behaviour tree entirely.  When movement input is detected the hero's
///   UnitPathFollower path is cancelled so the BT doesn't fight the manual movement.
///
/// SETUP:
///   Add this component to any persistent scene GameObject (e.g. GameManager).
///   No other wiring required — it reads CameraController.Instance automatically.
///
/// NOTES:
///   • The BT keeps ticking while manual control is active.  If the hero spots an
///     enemy or the BT selects a new path target, the BT will try to move the hero
///     again on the next tick.  Hold a direction to override it continuously, or
///     disable the hero's BehaviorTreeRunner component if you want pure manual control.
///   • Speed is read from the hero's MovementComponent so it matches the unit's stats.
///   • Tab still cycles the camera target normally (handled by CameraController).
///   • Set ManualControlKey in the Inspector to toggle manual control on/off for the
///     current hero, or leave it as None to always allow WASD input.
/// </summary>
public class PlayerControl : MonoBehaviour
{
    [Header("Controls")]
    [Tooltip("Optional toggle key. Press to enable/disable manual control for the " +
             "current hero. Leave as None to always allow WASD movement.")]
    [SerializeField] private KeyCode manualControlToggleKey = KeyCode.None;

    [Tooltip("Speed multiplier applied on top of the hero's base MovementComponent speed. " +
             "1 = normal speed, 1.5 = 50% faster than AI pathing speed.")]
    [SerializeField] private float speedMultiplier = 1f;

    [Tooltip("If true, pressing any WASD key automatically cancels the hero's current " +
             "AI path so the BT doesn't immediately override manual input.")]
    [SerializeField] private bool cancelPathOnInput = true;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _manualControlEnabled = true;   // starts enabled if no toggle key is set

    // The hero we cancelled the path on last frame — used to avoid spamming StopPath.
    private BaseHero _lastControlledHero = null;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Update()
    {
        // Toggle manual control
        if (manualControlToggleKey != KeyCode.None &&
            Input.GetKeyDown(manualControlToggleKey))
        {
            _manualControlEnabled = !_manualControlEnabled;

            if (!_manualControlEnabled)
                _lastControlledHero = null;
        }

        if (!_manualControlEnabled) return;

        // Find the hero the camera is currently locked onto
        BaseHero currentHero = CameraController.Instance?.CurrentHero;
        if (currentHero == null) return;

        // Read WASD / arrow input
        float h = 0f, v = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    v += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  v -= 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  h -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;

        bool hasInput = h != 0f || v != 0f;

        if (!hasInput)
        {
            _lastControlledHero = null;
            return;
        }

        // Cancel the AI path on the first frame of input (or when the hero changes)
        if (cancelPathOnInput && currentHero != _lastControlledHero)
        {
            currentHero.GetComponent<UnitPathFollower>()?.StopPath();
            _lastControlledHero = currentHero;
        }

        // Resolve movement speed from the hero's own stats
        float speed = 3f;   // fallback if no MovementComponent
        var mc = currentHero.GetComponent<MovementComponent>();
        if (mc != null) speed = mc.movement_Speed;
        speed *= speedMultiplier;

        // Move the hero
        Vector3 dir = new Vector3(h, v, 0f).normalized;
        currentHero.transform.position += dir * speed * Time.deltaTime;
    }


}
