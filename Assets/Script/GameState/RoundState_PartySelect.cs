using UnityEngine;

/// <summary>
/// Game state that runs while the player is picking their party.
///
/// Flow:
///   Enter  → show the PartySelectionUI
///   Confirm (called by PartySelectionUI when the player presses Start) →
///            generate dungeon → switch to InGame
///   Exit   → hide the PartySelectionUI
///
/// On lose the game returns here so the player can draft a new team.
/// On win the state is NOT re-entered — the same party continues to the next floor.
/// </summary>
public class RoundState_PartySelect : RoundState_Base
{
    private GameManager _gm;

    public override void EnterState(GameManager gm)
    {
        _gm = gm;
        Debug.Log("[RoundState_PartySelect] Showing party selection.");
        PartySelectionUI.Instance?.Show();
    }

    public override void ExitState(GameManager gm)
    {
        PartySelectionUI.Instance?.Hide();
    }

    public override void UpdateState(GameManager gm) { }

    /// <summary>
    /// Called by PartySelectionUI when the player confirms their party.
    /// Validates the selection, generates the dungeon, and enters gameplay.
    /// </summary>
    public void Confirm()
    {
        if (PartyData.Instance == null || !PartyData.Instance.IsPartyValid())
        {
            Debug.LogWarning("[RoundState_PartySelect] No heroes selected — cannot start.");
            return;
        }

        Debug.Log($"[RoundState_PartySelect] Party confirmed ({PartyData.Instance.SelectedParty.Count} heroes). Generating dungeon…");

        // Reset floor progression so each fresh run starts at floor 1.
        // (On a Win flow the player never reaches PartySelect, so this only
        //  fires after a wipe — safe to reset unconditionally here.)
        RunProgressionManager.Instance?.ResetProgress();

        // Generate a fresh dungeon. DungeonSpawner.SpawnAll() fires automatically
        // once GridGenerator.OnGridGenerated fires at end-of-frame.
        DungeonGenerator.Instance?.Regenerate();
        GridGenerator.Instance?.RegenerateGrid();

        _gm.SwitchState(_gm.InGame);
    }
}
