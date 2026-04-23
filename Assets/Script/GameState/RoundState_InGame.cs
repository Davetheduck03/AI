using UnityEngine;

/// <summary>
/// Active gameplay state.  All game logic runs via Unity's own Update loops
/// on the individual components — this state simply acts as a marker.
/// EnterState is the hook used by other states to kick off the dungeon run;
/// ExitState and UpdateState are intentional no-ops.
/// </summary>
public class RoundState_InGame : RoundState_Base
{
    public override void EnterState(GameManager round) { FloorHUD.Show(); }
    public override void ExitState(GameManager round)  { FloorHUD.Hide(); }
    public override void UpdateState(GameManager round){ }
}
