public class ReturningState : IState
{
    private readonly CompanionController companion;

    public ReturningState(CompanionController companion)
    {
        this.companion = companion;
    }

    public void Enter()
    {
        companion.Agent.SetDestination(companion.PlayerTransform.position);
    }

    public void Tick()
    {
        bool stillCalculatingPath = companion.Agent.pathPending;
        bool arrived = companion.Agent.remainingDistance <= companion.ArrivalThreshold;

        if (!stillCalculatingPath && arrived)
        {
            companion.ChangeState(companion.IdleState);
        }
    }

    public void Exit()
    {
    }
}
