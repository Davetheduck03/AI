using UnityEngine;

/// <summary>
/// Triggered when the entire party is wiped.
///
/// Flow:
///   1. Capture the current floor stats from RunProgressionManager.
///   2. Show the GameOverScreen overlay — the player sees how many floors
///      they reached/cleared before the world is torn down.
///   3. GameOverScreen.OnPlayAgain() performs the actual cleanup, resets
///      progression, and navigates back to party selection.
///
/// The dungeon is NOT cleaned up here — it stays visible behind the overlay
/// so the player can see the world they died in while reading the score.
/// Cleanup happens inside GameOverScreen when the player presses Play Again.
/// </summary>
public class RoundState_Lose : RoundState_Base
{
    public override void EnterState(GameManager gm)
    {
        var prog = RunProgressionManager.Instance;
        int floorsCompleted = prog?.FloorsCompleted ?? 0;
        int floorDiedOn     = prog?.FloorNumber     ?? 1;

        Debug.Log($"[RoundState_Lose] Party wiped on floor {floorDiedOn} " +
                  $"({floorsCompleted} floor(s) cleared). Showing game over screen.");

        // Show the overlay — actual reset is deferred until the player clicks Play Again.
        GameOverScreen.Show(floorsCompleted, floorDiedOn);
    }

    public override void ExitState(GameManager gm)  { }
    public override void UpdateState(GameManager gm) { }
}
