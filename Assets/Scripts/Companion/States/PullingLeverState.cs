using UnityEngine;

public class PullingLeverState : IState
{
    private const float PullDurationSeconds = 1.5f;

    private readonly CompanionController companion;
    private float elapsed;

    public PullingLeverState(CompanionController companion)
    {
        this.companion = companion;
    }

    public void Enter()
    {
        elapsed = 0f;
        companion.TriggerLever();
    }

    public void Tick()
    {
        elapsed += Time.deltaTime;

        if (elapsed >= PullDurationSeconds)
        {
            companion.ChangeState(companion.ReturningState);
        }
    }

    public void Exit()
    {
    }
}
