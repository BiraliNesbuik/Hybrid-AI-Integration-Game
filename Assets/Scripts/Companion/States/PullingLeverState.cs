public class PullingLeverState : IState
{
    private readonly CompanionController companion;

    public PullingLeverState(CompanionController companion)
    {
        this.companion = companion;
    }

    public void Enter()
    {
        companion.TriggerLever();
    }

    public void Tick()
    {
        if (companion.ObstacleCleared)
        {
            companion.ChangeState(companion.ReturningState);
        }
    }

    public void Exit()
    {
    }
}
