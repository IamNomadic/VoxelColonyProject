using UnityEngine;

[CreateAssetMenu(menuName = "Pawn/Behaviours/Grid Wander Logic")]
public class GridWanderBehaviour : PawnStateBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 2.0f;
    public float waitTime = 1.0f;
    public bool alignToCenter = true;
    public float verticalOffset = 0.0f;

    [Header("Physics")]
    public LayerMask obstacleMask;
    public LayerMask groundMask;

    // Runtime
    private bool isWaiting;
    private Vector3 startOrigin;

    public override void Enter(PawnContext ctx)
    {
        Debug.Log($"[Pawn] ENTER Wander State on {ctx.gameObject.name}");

        // Force setup
        if (ctx.rb != null) ctx.rb.isKinematic = true;
        if (ctx.animator != null) ctx.animator.applyRootMotion = false;

        if (startOrigin == Vector3.zero) startOrigin = ctx.transform.position;

        isWaiting = false;
        PickNewTarget(ctx);
    }

    public override void Execute(PawnContext ctx)
    {
        if (isWaiting)
        {
            if (Time.time > ctx.stateTimer)
            {
                Debug.Log("[Pawn] Wait finished. Picking new target...");
                isWaiting = false;
                PickNewTarget(ctx);
            }
        }
        else
        {
            Move(ctx);
        }
    }

    public override void Exit(PawnContext ctx)
    {
        Debug.Log("[Pawn] EXIT Wander State");
        ctx.transform.position = ctx.currentGridTarget;
    }

    private void Move(PawnContext ctx)
    {
        Vector3 current = ctx.transform.position;
        Vector3 target = ctx.currentGridTarget;

        float step = moveSpeed * Time.deltaTime;
        if (step > 0.5f) step = 0.5f;

        ctx.transform.position = Vector3.MoveTowards(current, target, step);

        // Rotate
        Vector3 dir = (target - current);
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            ctx.transform.rotation = Quaternion.Slerp(ctx.transform.rotation, rot, 10f * Time.deltaTime);
        }

        if (Vector3.Distance(ctx.transform.position, target) < 0.01f)
        {
            Debug.Log("[Pawn] Reached Target. Waiting...");
            ctx.transform.position = target;
            isWaiting = true;
            ctx.stateTimer = Time.time + waitTime;
        }
    }

    private void PickNewTarget(PawnContext ctx)
    {
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        for (int i = 0; i < 10; i++)
        {
            Vector3 dir = dirs[Random.Range(0, dirs.Length)];

            // 1. Get base coord
            Vector3 flatCandidate = GetGridCoordinate(ctx.transform.position + dir);

            // 2. Check Flat
            if (IsValid(flatCandidate))
            {
                SetTarget(ctx, flatCandidate, "Flat");
                return;
            }

            // 3. Check Up
            Vector3 upCandidate = flatCandidate + Vector3.up;
            if (IsValid(upCandidate))
            {
                SetTarget(ctx, upCandidate, "Up");
                return;
            }

            // 4. Check Down
            Vector3 downCandidate = flatCandidate + Vector3.down;
            if (IsValid(downCandidate))
            {
                SetTarget(ctx, downCandidate, "Down");
                return;
            }
        }

        Debug.LogWarning("[Pawn] FAILED to find target. Waiting...");
        isWaiting = true;
        ctx.stateTimer = Time.time + 1.0f;
    }

    private void SetTarget(PawnContext ctx, Vector3 t, string type)
    {
        Debug.Log($"[Pawn] Target Found ({type}): {t}");
        ctx.currentGridTarget = t;

        // Visual Debug Line
        Debug.DrawLine(ctx.transform.position, t, Color.green, 2.0f);
    }

    private Vector3 GetGridCoordinate(Vector3 rawPos)
    {
        float x = alignToCenter ? Mathf.Floor(rawPos.x) + 0.5f : Mathf.Round(rawPos.x);
        float z = alignToCenter ? Mathf.Floor(rawPos.z) + 0.5f : Mathf.Round(rawPos.z);
        float y = Mathf.Round(rawPos.y - verticalOffset) + verticalOffset;
        return new Vector3(x, y, z);
    }

    private bool IsValid(Vector3 target)
    {
        // Head Check
        if (Physics.CheckSphere(target + Vector3.up * 0.25f, 0.3f, obstacleMask)) return false;

        // Foot Check
        if (!Physics.Raycast(target + Vector3.up * 0.1f, Vector3.down, 0.6f, groundMask)) return false;

        return true;
    }
}

/*
### **Step 2: The "Why Is It Not Logging?" Checklist**

If you paste that script and **still see zero logs**, the issue is in your **PawnStateMachine**.

1.  **Check the Script Assignment:**
    *Select the Pawn GameObject.
    * Does it have the `PawnStateMachine` component?
    * Is the **Initial State** slot filled? (It must not be "None").
    *Does that Initial State Asset have the **Behaviour** slot filled?

2.  **Check the `Awake` Method:**
    *Open `PawnStateMachine.cs`.
    *Ensure `Start` or `Awake` actually calls `TransitionTo(initialState)`.
    *If your `Awake` looks like this, it is correct:
      ```csharp

      void Awake()
{
    // ... setup ...
    if (initialState != null) TransitionTo(initialState); // THIS LINE MUST RUN
    else Debug.LogError("No Initial State assigned!");
}

*/