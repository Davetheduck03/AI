using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundState_Start : RoundState_Base
{
    public override void EnterState(GameManager round)
    {
        // Hand off immediately to the party selection screen
        round.SwitchState(round.PartySelect);
    }

    public override void ExitState(GameManager round)
    {
        
    }

    public override void UpdateState(GameManager round)
    {
        
    }
}
