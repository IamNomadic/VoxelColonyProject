using UnityEngine;

[RequireComponent(typeof(PawnIdentity))]
[RequireComponent(typeof(Rigidbody))]
public class PawnStateMachine : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private PawnStateSO initialState;

    private PawnStateSO currentStateInstance;
    private PawnContext ctx;

    void Awake()
    {
        var world = FindObjectOfType<VoxelWorld>();
        var rb = GetComponent<Rigidbody>();
        var anim = GetComponent<Animator>();
        var id = GetComponent<PawnIdentity>();

        // Create Blackboard with Identity
        ctx = new PawnContext(transform, rb, world, anim, id);

        // Snap to Grid immediately on spawn to align with voxels
        SnapToGrid();

        if (initialState != null)
            TransitionTo(initialState);
    }

    void Update()
    {
        if (currentStateInstance == null) return;

        // Check Transitions
        if (currentStateInstance.Transitions != null)
        {
            foreach (var t in currentStateInstance.Transitions)
            {
                bool allTrue = true;
                if (t.Conditions != null)
                {
                    foreach (var cond in t.Conditions)
                    {
                        if (!cond.Evaluate(ctx))
                        {
                            allTrue = false;
                            break;
                        }
                    }
                }

                if (allTrue && t.NextState != null)
                {
                    TransitionTo(t.NextState);
                    return;
                }
            }
        }

        // Execute State
        if (currentStateInstance.Behaviour != null)
            currentStateInstance.Behaviour.Execute(ctx);
    }

    public void TransitionTo(PawnStateSO nextState)
    {
        if (currentStateInstance != null && currentStateInstance.Behaviour != null)
            currentStateInstance.Behaviour.Exit(ctx);

        ctx.lastTransitionTime = Time.time;

        // Create deep copy of state so values don't overlap between enemies
        currentStateInstance = Instantiate(nextState);
        if (nextState.Behaviour != null)
        {
            currentStateInstance.Behaviour = Instantiate(nextState.Behaviour);
        }

        if (currentStateInstance.Behaviour != null)
            currentStateInstance.Behaviour.Enter(ctx);
    }

    void SnapToGrid()
    {
        // Snap to center of block (0.5 on X/Z, 0 on Y for feet)
        Vector3 snapped = new Vector3(
            Mathf.Floor(transform.position.x) + 0.5f,
            Mathf.Floor(transform.position.y),
            Mathf.Floor(transform.position.z) + 0.5f
        );
        transform.position = snapped;
        ctx.currentGridTarget = snapped;
    }
}