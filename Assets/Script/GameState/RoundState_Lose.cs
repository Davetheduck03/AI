using UnityEngine;

/// <summary>
/// Triggered when the hero dies.
/// 1. Cleans up all spawned objects and world items.
/// 2. Regenerates the dungeon layout (new tiles).
/// 3. Rebuilds the pathfinding grid from the new tiles.
/// 4. GridGenerator.OnGridGenerated fires at end-of-frame → DungeonSpawner re-spawns everything.
/// 5. Immediately transitions back to InGame.
///
/// NOTE: If you have a FogOfWarManager with a Reset() method, call it here
/// between CleanupAll() and Regenerate() so the fog resets to fully hidden.
/// </summary>
public class RoundState_Lose : RoundState_Base
{
    public override void EnterState(GameManager gm)
    {
        Debug.Log("[RoundState_Lose] Hero died — regenerating dungeon.");

        // 1. Destroy all heroes, enemies, chests, and world items
        DungeonSpawner.Instance?.CleanupAll();

        // 2. Clear the party selection so the player drafts a fresh team.
        //    (Dungeon regeneration happens when they confirm their new party.)
        PartyData.Instance?.ClearParty();

        // 3. Back to party selection
        gm.SwitchState(gm.PartySelect);
    }

    public override void ExitState(GameManager gm)  { }
    public override void UpdateState(GameManager gm) { }
}
