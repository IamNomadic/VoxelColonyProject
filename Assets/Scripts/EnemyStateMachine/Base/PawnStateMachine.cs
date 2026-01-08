using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PawnStateMachine : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private PawnDataSO pawnData;
    [SerializeField] private PawnStateSO initialState;

    // Debug view
    public PawnStateSO CURRENTSTATE;
    public PawnDataSO Data => pawnData;

    private PawnStateSO currentStateInstance;
    private PawnContext ctx;
    private bool hasLanded = false;

    void Awake()
    {
        if (pawnData == null)
        {
            Debug.LogError($"Pawn {name} is missing PawnDataSO!");
            enabled = false;
            return;
        }

        var world = FindObjectOfType<VoxelWorld>();
        var rb = GetComponent<Rigidbody>();
        var anim = GetComponent<Animator>();

        ctx = new PawnContext(transform, rb, world, anim, pawnData); // Initialize Context
        UpdateColor();
    }

    // --- NEW: COMMANDER API ---
    public void QueueCommand(Vector3 target)
    {
        if (ctx != null)
        {
            ctx.commandQueue.Enqueue(target);
            Debug.Log($"{name} received order. Queue size: {ctx.commandQueue.Count}");
        }
    }

    public void ClearCommands()
    {
        if (ctx != null)
        {
            ctx.commandQueue.Clear();
            ctx.currentCommandTarget = null;
        }
    }
    // ---------------------------

    void Update()
    {
        CURRENTSTATE = currentStateInstance;
        if (!hasLanded)
        {
            HandleGravity();
            return;
        }

        if (currentStateInstance == null) return;

        // Transition Logic
        if (currentStateInstance.Transitions != null)
        {
            foreach (var t in currentStateInstance.Transitions)
            {
                bool allTrue = true;
                if (t.Conditions != null)
                {
                    foreach (var cond in t.Conditions)
                    {
                        if (!cond.Evaluate(ctx)) { allTrue = false; break; }
                    }
                }

                if (allTrue && t.NextState != null)
                {
                    TransitionTo(t.NextState);
                    return;
                }
            }
        }

        if (currentStateInstance.Behaviour != null)
            currentStateInstance.Behaviour.Execute(ctx);
    }

    private void HandleGravity()
    {
        transform.position += Vector3.down * pawnData.gravitySpeed * Time.deltaTime;

        // Gravity/Landing Logic
        if (VoxelPathHelper.IsWalkable(ctx.world, transform.position, checkPawns: false))
        {
            hasLanded = true;
            ctx.SetTargetAndSnap(transform.position);
            transform.position = ctx.currentGridTarget;

            if (initialState != null) TransitionTo(initialState);
        }

        if (transform.position.y < -50) { hasLanded = true; enabled = false; }
    }

    public void TransitionTo(PawnStateSO nextState)
    {
        if (currentStateInstance != null && currentStateInstance.Behaviour != null)
            currentStateInstance.Behaviour.Exit(ctx);

        ctx.lastTransitionTime = Time.time;

        currentStateInstance = Instantiate(nextState);
        if (nextState.Behaviour != null)
            currentStateInstance.Behaviour = Instantiate(nextState.Behaviour);

        if (currentStateInstance.Behaviour != null)
            currentStateInstance.Behaviour.Enter(ctx);
    }

    void UpdateColor()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null) rend = GetComponentInChildren<Renderer>();
        if (rend != null) rend.material.color = pawnData.debugColor;
    }

    private void OnDrawGizmosSelected()
    {
        if (pawnData != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, pawnData.sightRadius);
        }
    }
}