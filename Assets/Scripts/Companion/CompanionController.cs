using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class CompanionController : MonoBehaviour
{
    [SerializeField] private Transform leverTarget;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float arrivalThreshold = 0.5f;
    [SerializeField] private LayerMask sensorObstacleMask;
    [SerializeField] private UnityEvent onLeverPulled;

    public NavMeshAgent Agent { get; private set; }
    public Transform LeverTarget => leverTarget;
    public Transform PlayerTransform => playerTransform;
    public float ArrivalThreshold => arrivalThreshold;

    public IState IdleState { get; private set; }
    public IState MovingToLeverState { get; private set; }
    public IState VerifyingSpotState { get; private set; }
    public IState PullingLeverState { get; private set; }
    public IState ReturningState { get; private set; }

    private StateMachine stateMachine;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        stateMachine = new StateMachine();

        IdleState = new IdleState(this);
        MovingToLeverState = new MovingToLeverState(this);
        VerifyingSpotState = new VerifyingSpotState(this);
        PullingLeverState = new PullingLeverState(this);
        ReturningState = new ReturningState(this);
    }

    private void Start()
    {
        ChangeState(IdleState);
    }

    private void Update()
    {
        stateMachine.Tick();

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            CommandToLever();
        }
    }

    public void ChangeState(IState newState)
    {
        stateMachine.ChangeState(newState);
    }

    public void CommandToLever()
    {
        if (stateMachine.CurrentState == IdleState)
        {
            ChangeState(MovingToLeverState);
        }
    }

    public bool HasClearPathTo(Vector3 targetPosition)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 toTarget = targetPosition - origin;
        float distance = toTarget.magnitude;

        if (distance <= 0.01f)
        {
            return true;
        }

        Vector3 direction = toTarget / distance;
        return !Physics.Raycast(origin, direction, distance, sensorObstacleMask);
    }

    public void TriggerLever()
    {
        onLeverPulled?.Invoke();
    }
}
