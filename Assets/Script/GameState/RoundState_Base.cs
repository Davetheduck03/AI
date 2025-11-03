using UnityEngine;

public abstract class RoundState_Base
{
    public abstract void EnterState(GameManager round);
    public abstract void UpdateState(GameManager round);
    public abstract void ExitState(GameManager round);

}
