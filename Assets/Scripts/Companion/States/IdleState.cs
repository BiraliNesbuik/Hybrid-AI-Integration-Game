public class IdleState : IState
{
    private const float FollowStopDistance = 3f;

    private readonly CompanionController companion;

    public IdleState(CompanionController companion)
    {
        this.companion = companion;
    }

    public void Enter()
    {
    }

    public void Tick()
    {
        float distanceToPlayer = UnityEngine.Vector3.Distance(
            companion.transform.position,
            companion.PlayerTransform.position);

        if (distanceToPlayer > FollowStopDistance)
        {
            companion.Agent.SetDestination(companion.PlayerTransform.position);
        }
        else
        {
            companion.Agent.ResetPath();
        }
    }

    public void Exit()
    {
    }
}
