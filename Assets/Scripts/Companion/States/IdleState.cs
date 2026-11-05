using UnityEngine;
using UnityEngine.AI;

public class IdleState : IState
{
    private const float FollowStopDistance = 3f;

    // Last-resort fallback for the known broken-NavMesh spots (stairs etc.)
    // we deliberately left unfixed. Only used for following the player -
    // the required lever/obstacle task never uses this, it stays pure NavMesh.
    private const float StuckGiveUpSeconds = 8f;
    private const float StuckVelocityThreshold = 0.05f;

    private readonly CompanionController companion;
    private float stuckTimer;

    public IdleState(CompanionController companion)
    {
        this.companion = companion;
    }

    public void Enter()
    {
        stuckTimer = 0f;
    }

    public void Tick()
    {
        if (!companion.FollowPlayer)
        {
            companion.Agent.ResetPath();
            return;
        }

        float distanceToPlayer = Vector3.Distance(
            companion.transform.position,
            companion.PlayerTransform.position);

        if (distanceToPlayer <= FollowStopDistance)
        {
            companion.Agent.ResetPath();
            stuckTimer = 0f;
            return;
        }

        companion.Agent.SetDestination(companion.PlayerTransform.position);

        // Measure whether Bindik himself is actually making progress, not
        // whether the path is still being (re)calculated - the player moving
        // every frame would otherwise keep resetting a path-status check.
        bool barelyMoving = companion.Agent.velocity.magnitude < StuckVelocityThreshold;

        if (!barelyMoving)
        {
            stuckTimer = 0f;
            return;
        }

        stuckTimer += Time.deltaTime;

        if (stuckTimer >= StuckGiveUpSeconds)
        {
            TeleportToPlayer();
            stuckTimer = 0f;
        }
    }

    private void TeleportToPlayer()
    {
        Transform player = companion.PlayerTransform;
        Vector3 besidePlayer = player.position + player.right * 1.5f;

        if (NavMesh.SamplePosition(besidePlayer, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            companion.Agent.Warp(hit.position);
            return;
        }

        // Fallback: the spot beside the player was not on the NavMesh, so
        // land on the player's own position instead of not teleporting at all.
        if (NavMesh.SamplePosition(player.position, out NavMeshHit fallbackHit, 5f, NavMesh.AllAreas))
        {
            companion.Agent.Warp(fallbackHit.position);
        }
    }

    public void Exit()
    {
    }
}
