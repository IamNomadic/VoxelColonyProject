using UnityEngine;

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

        // Create Blackboard
        ctx = new PawnContext(transform, rb, world, anim);

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
        // Assume 0.5 offset for Cube
        float yOffset = 0.5f;
        Vector3 snapped = new Vector3(
            Mathf.Round(transform.position.x),
            Mathf.Round(transform.position.y - yOffset) + yOffset,
            Mathf.Round(transform.position.z)
        );
        transform.position = snapped;
        ctx.currentGridTarget = snapped;
    }
}