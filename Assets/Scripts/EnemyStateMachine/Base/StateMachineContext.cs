using UnityEngine;

public class PawnContext
{
    // References
    public Transform transform;
    public GameObject gameObject;
    public Rigidbody rb;
    public Animator animator;
    public VoxelWorld world;

    // Runtime Data
    public Vector3 currentGridTarget;
    public float stateTimer;
    public float lastTransitionTime;

    public PawnContext(Transform t, Rigidbody r, VoxelWorld w, Animator a)
    {
        transform = t;
        gameObject = t.gameObject;
        rb = r;
        world = w;
        animator = a;
    }
}