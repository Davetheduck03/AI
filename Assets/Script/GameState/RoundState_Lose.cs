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

        // 1. Destroy enemies, chests, world items, and the old hero instance
        DungeonSpawner.Instance?.CleanupAll();

        // 2. TODO: FogOfWarManager.Instance?.ResetFog();
        //    Uncomment and implement if you want fog to reset each round.

        // 3. Paint a fresh dungeon onto the tilemaps
        DungeonGenerator.Instance?.Regenerate();

        // 4. Rebuild pathfinding nodes from the new tiles.
        //    This fires OnGridGenerated at end-of-frame → DungeonSpawner.SpawnAll() runs.
        GridGenerator.Instance?.RegenerateGrid();

        // 5. Back to gameplay immediately (spawning happens asynchronously via events)
        gm.SwitchState(gm.InGame);
    }

    public override void ExitState(GameManager gm)  { }
    public override void UpdateState(GameManager gm) { }
}
