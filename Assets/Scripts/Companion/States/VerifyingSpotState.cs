public class VerifyingSpotState : IState
{
    private readonly CompanionController companion;

    public VerifyingSpotState(CompanionController companion)
    {
        this.companion = companion;
    }

    public void Enter()
    {
    }

    public void Tick()
    {
        bool canReachLever = companion.HasClearPathTo(companion.LeverTarget.position);

        if (canReachLever)
        {
            companion.ChangeState(companion.PullingLeverState);
        }
        else
        {
            companion.ChangeState(companion.MovingToLeverState);
        }
    }

    public void Exit()
    {
    }
}
