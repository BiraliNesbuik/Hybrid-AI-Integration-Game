public class IdleState : IState
{
    private readonly CompanionController companion;

    public IdleState(CompanionController companion)
    {
        this.companion = companion;
    }

    public void Enter()
    {
        companion.Agent.ResetPath();
    }

    public void Tick()
    {
    }

    public void Exit()
    {
    }
}
