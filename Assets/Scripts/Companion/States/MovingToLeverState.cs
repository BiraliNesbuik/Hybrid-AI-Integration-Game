using UnityEngine;

public class MovingToLeverState : IState
{
    private readonly CompanionController companion;
    private float elapsed;

    public MovingToLeverState(CompanionController companion)
    {
        this.companion = companion;
    }

    public void Enter()
    {
        elapsed = 0f;
        companion.Agent.SetDestination(companion.LeverTarget.position);
    }

    public void Tick()
    {
        elapsed += Time.deltaTime;

        bool stillCalculatingPath = companion.Agent.pathPending;
        bool hasCompletePath = companion.Agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathComplete;
        bool arrived = hasCompletePath && companion.Agent.remainingDistance <= companion.ArrivalThreshold;

        if (!stillCalculatingPath && arrived)
        {
            companion.ChangeState(companion.VerifyingSpotState);
            return;
        }

        if (elapsed >= companion.GiveUpAfterSeconds)
        {
            Debug.Log($"[Test] BASARISIZ - {companion.GiveUpAfterSeconds} sn icinde hedefe ulasilamadi (pathStatus: {companion.Agent.pathStatus})");
            companion.Agent.ResetPath();
            companion.ChangeState(companion.IdleState);
        }
    }

    public void Exit()
    {
    }
}
