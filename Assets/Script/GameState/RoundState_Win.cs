using UnityEngine;

/// <summary>
/// Triggered when the hero reaches the extraction point carrying the Relic.
/// 1. Cleans up enemies, chests, and world items but KEEPS the hero alive with their gear.
/// 2. Resets the hero's RelicHolder so they start the next floor without the Relic.
/// 3. Regenerates the dungeon layout (new tiles, new rooms).
/// 4. Rebuilds the pathfinding grid → DungeonSpawner re-spawns enemies and chests.
/// 5. Immediately transitions back to InGame.
///
/// The hero retains their equipped items across floors — each floor is a harder run.
/// </summary>
public class RoundState_Win : RoundState_Base
{
    public override void EnterState(GameManager gm)
    {
        Debug.Log("[RoundState_Win] Hero extracted with the Relic — generating next floor.");

        // 1. Destroy enemies, chests, and world items; keep all surviving heroes alive.
        DungeonSpawner.Instance?.CleanupExceptHeroes();

        // 2. Paint a fresh dungeon onto the tilemaps.
        DungeonGenerator.Instance?.Regenerate();

        // 3. Rebuild pathfinding nodes from the new tiles.
        //    Fires OnGridGenerated at end-of-frame → DungeonSpawner.SpawnAll() runs.
        GridGenerator.Instance?.RegenerateGrid();

        // 4. Back to gameplay — spawning happens asynchronously via events.
        gm.SwitchState(gm.InGame);
    }

    public override void ExitState(GameManager gm)  { }
    public override void UpdateState(GameManager gm) { }
}
