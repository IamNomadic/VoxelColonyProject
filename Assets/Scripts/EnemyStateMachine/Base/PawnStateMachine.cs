using UnityEngine;

[RequireComponent(typeof(PawnIdentity))]
[RequireComponent(typeof(Rigidbody))]
public class PawnStateMachine : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private PawnStateSO initialState;
    [SerializeField] private float gravitySpeed = 15f; // How fast they fall

    private PawnStateSO currentStateInstance;
    private PawnContext ctx;
    private bool hasLanded = false; // <-- New Flag for gravity

    void Awake()
    {
        // We only SETUP references here. We do NOT look for ground yet.
        var world = FindObjectOfType<VoxelWorld>();
        var rb = GetComponent<Rigidbody>();
        var anim = GetComponent<Animator>();
        var id = GetComponent<PawnIdentity>();

        // Initialize Blackboard
        ctx = new PawnContext(transform, rb, world, anim, id);
    }

    void Update()
    {
        // PHASE 1: FALLING
        // If we haven't landed yet, do nothing but fall.
        if (!hasLanded)
        {
            HandleGravity();
            return;
        }

        // PHASE 2: AI LOGIC
        // Once landed, the state machine takes over.
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

    private void HandleGravity()
    {
        // 1. Move Down physically
        transform.position += Vector3.down * gravitySpeed * Time.deltaTime;

        // 2. Check if we hit valid ground
        // We use the Helper to check: "Is the block below me solid?"
        if (VoxelPathHelper.IsWalkable(ctx.world, transform.position, checkPawns: false))
        {
            // We found ground! 
            hasLanded = true;

            // Snap strictly to the grid + offset to stop the falling cleanly
            ctx.SetTargetAndSnap(transform.position);
            transform.position = ctx.currentGridTarget;

            // Now we can start the AI Brain
            if (initialState != null)
                TransitionTo(initialState);
        }

        // Safety: If we fell into the void (Chunk failed to load), stop falling eventually
        if (transform.position.y < -50)
        {
            // Optional: Respawn or Destroy
            hasLanded = true; // Stop processing gravity
            enabled = false;  // Disable script
        }
    }

    public void TransitionTo(PawnStateSO nextState)
    {
        if (currentStateInstance != null && currentStateInstance.Behaviour != null)
            currentStateInstance.Behaviour.Exit(ctx);

        ctx.lastTransitionTime = Time.time;

        currentStateInstance = Instantiate(nextState);
        if (nextState.Behaviour != null)
        {
            currentStateInstance.Behaviour = Instantiate(nextState.Behaviour);
        }

        if (currentStateInstance.Behaviour != null)
            currentStateInstance.Behaviour.Enter(ctx);
    }
}