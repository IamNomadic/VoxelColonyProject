using UnityEngine;

// Removed [RequireComponent(typeof(PawnIdentity))]
[RequireComponent(typeof(Rigidbody))]
public class PawnStateMachine : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Drag the Data Profile (Stats/Species) here.")]
    [SerializeField] private PawnDataSO pawnData;

    [Tooltip("The AI State Logic.")]
    [SerializeField] private PawnStateSO initialState;
    public PawnStateSO CURRENTSTATE;
    // Public Accessor for Sensors (So other pawns can read my type)
    public PawnDataSO Data => pawnData;

    // Internal
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

        // Initialize Context with DATA
        ctx = new PawnContext(transform, rb, world, anim, pawnData);

        // Apply Debug Color immediately
        UpdateColor();
    }

    void Update()
    {
        CURRENTSTATE = currentStateInstance;
        if (!hasLanded)
        {
            HandleGravity();
            return;
        }

        if (currentStateInstance == null) return;

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
        // Read gravity speed from DATA
        transform.position += Vector3.down * pawnData.gravitySpeed * Time.deltaTime;

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