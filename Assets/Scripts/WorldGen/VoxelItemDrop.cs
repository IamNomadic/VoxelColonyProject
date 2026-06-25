
using UnityEngine;

public class VoxelItemDrop : MonoBehaviour
{
    public BlockData blockData;

    private Transform visualTransform;
    private Rigidbody rb;
    private BoxCollider col;

    private float spawnTime;
    private Transform playerTransform;

    public void Initialize(BlockData data, Vector3 spawnPos)
    {
        blockData = data;
        transform.position = spawnPos;
        spawnTime = Time.time;

        // 1. SETUP PHYSICS (Root Object)
        rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.linearDamping = 1f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Freeze rotation so it stands perfectly upright while falling
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        col = gameObject.AddComponent<BoxCollider>();
        col.size = new Vector3(0.25f, 0.25f, 0.25f);

        // Put on Ignore Raycast layer (Layer 2) so the player's mining clicks pass through it
        gameObject.layer = 2;

        // 2. SETUP VISUALS (Child Object)
        GameObject visuals = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(visuals.GetComponent<Collider>()); // Remove default cube collider

        visualTransform = visuals.transform;
        visualTransform.SetParent(transform);
        visualTransform.localPosition = Vector3.zero;

        // Scale it down to be a mini-block
        visualTransform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

        // Apply Block Appearance
        MeshRenderer rend = visuals.GetComponent<MeshRenderer>();
        if (data.blockMaterial != null)
        {
            // Instantiate a new material instance so we can change its color safely
            rend.material = new Material(data.blockMaterial);
        }
        rend.material.color = data.blockColor;

        // 3. POP EFFECT
        // Give it a random slight bump upwards and outwards so it pops out of the wall
        Vector3 popDir = Vector3.up + Random.insideUnitSphere * 0.3f;
        rb.AddForce(popDir.normalized * 3f, ForceMode.Impulse);

        // 4. FIND PLAYER FOR PICKUP
        PlayerMovement pm = FindObjectOfType<PlayerMovement>();
        if (pm != null) playerTransform = pm.transform;
    }

    void Update()
    {
        if (visualTransform != null)
        {
            // --- ANIMATION ---
            // 1. Spin in a circle
            visualTransform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);

            // 2. Bob up and down using a Sine Wave
            float newY = Mathf.Sin((Time.time - spawnTime) * 3f) * 0.1f;
            visualTransform.localPosition = new Vector3(0, newY, 0);
        }

        // --- SIMPLE PICKUP LOGIC ---
        // Prevents picking it up for the first 0.5 seconds so it can pop out first
        if (playerTransform != null && Time.time > spawnTime + 0.5f)
        {
            // If the player steps close to the item, "pick it up" (Destroy it)
            if (Vector3.Distance(transform.position, playerTransform.position) < 1.5f)
            {
                // NOTE: Here is where you would add the item to your inventory later!
                Destroy(gameObject);
            }
        }
    }
}