public class MovingToLeverState : IState
{
    private readonly CompanionController companion;

    public MovingToLeverState(CompanionController companion)
    {
        this.companion = companion;
    }

    public void Enter()
    {
        companion.Agent.SetDestination(companion.LeverTarget.position);
    }

    public void Tick()
    {
        bool stillCalculatingPath = companion.Agent.pathPending;
        bool hasCompletePath = companion.Agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathComplete;
        bool arrived = hasCompletePath && companion.Agent.remainingDistance <= companion.ArrivalThreshold;

        if (!stillCalculatingPath && arrived)
        {
            companion.ChangeState(companion.VerifyingSpotState);
        }
    }

    public void Exit()
    {
    }
}
