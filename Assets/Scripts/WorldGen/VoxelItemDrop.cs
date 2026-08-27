
using UnityEngine;

public class VoxelItemDrop : MonoBehaviour
{
    public BlockData blockData;
    public int itemCount = 1;
    public int currentDurability = 0;

    private Transform visualTransform;
    private Rigidbody rb;
    private BoxCollider col;

    private float spawnTime;
    private Vector3 savedVelocity;
    private bool isPhysicsPaused = false;

    public void Initialize(BlockData data, Vector3 spawnPos, int count = 1, int durability = -1)
    {
        blockData = data;
        itemCount = count;
        transform.position = spawnPos;

        spawnTime = SimulationClock.Instance.SimulationTime;

        if (durability == -1 && data.maxToolUses > 0) currentDurability = data.maxToolUses;
        else currentDurability = durability == -1 ? 0 : durability;

        rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.linearDamping = 1f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        col = gameObject.AddComponent<BoxCollider>();
        col.size = new Vector3(0.25f, 0.25f, 0.25f);
        gameObject.layer = 2; // Ignore Raycast

        GameObject visuals = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(visuals.GetComponent<Collider>());

        visualTransform = visuals.transform;
        visualTransform.SetParent(transform);
        visualTransform.localPosition = Vector3.zero;

        Vector3 extents = data.boundsMax - data.boundsMin;
        float scale = 0.25f + (Mathf.Clamp(count, 1, 64) / 64f) * 0.15f;
        visualTransform.localScale = new Vector3(scale * extents.x, scale * extents.y, scale * extents.z);

        MeshFilter mf = visuals.GetComponent<MeshFilter>();
        if (mf != null && mf.mesh != null)
        {
            Mesh m = mf.mesh;
            Color[] colors = new Color[m.vertices.Length];

            Color c = data.tintWithBlockColor ? data.blockColor : Color.white;
            if (c.a == 0) c.a = 1f;

            for (int i = 0; i < colors.Length; i++) colors[i] = c;
            m.colors = colors;

            Vector2[] uvs = m.uv;
            Vector3[] normals = m.normals;
            float uvSz = 1f / BlockManager.Instance.atlasGridSize;

            for (int i = 0; i < uvs.Length; i++)
            {
                Vector3 norm = new Vector3(Mathf.Round(normals[i].x), Mathf.Round(normals[i].y), Mathf.Round(normals[i].z));
                Vector2 gridUV = data.GetUV(norm);
                Vector2 atlasUV = new Vector2(gridUV.x * uvSz, gridUV.y * uvSz);

                float localU = uvs[i].x;
                float localV = uvs[i].y;

                if (Mathf.Abs(norm.y) > 0.5f) { localU = Mathf.Lerp(data.boundsMin.x, data.boundsMax.x, uvs[i].x); localV = Mathf.Lerp(data.boundsMin.z, data.boundsMax.z, uvs[i].y); }
                else if (Mathf.Abs(norm.z) > 0.5f) { localU = Mathf.Lerp(data.boundsMin.x, data.boundsMax.x, uvs[i].x); localV = Mathf.Lerp(data.boundsMin.y, data.boundsMax.y, uvs[i].y); }
                else { localU = Mathf.Lerp(data.boundsMin.z, data.boundsMax.z, uvs[i].x); localV = Mathf.Lerp(data.boundsMin.y, data.boundsMax.y, uvs[i].y); }

                uvs[i] = new Vector2(atlasUV.x + (localU * uvSz), atlasUV.y + (localV * uvSz));
            }
            m.uv = uvs;
        }

        MeshRenderer rend = visuals.GetComponent<MeshRenderer>();
        Material mat = null;

        if (data.blockMaterial != null) mat = new Material(data.blockMaterial);
        else if (data.isTransparent && BlockManager.Instance != null && BlockManager.Instance.transparentMaterial != null) mat = new Material(BlockManager.Instance.transparentMaterial);
        else if (BlockManager.Instance != null && BlockManager.Instance.worldMaterial != null) mat = new Material(BlockManager.Instance.worldMaterial);
        else
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            mat = urpShader != null ? new Material(urpShader) : new Material(Shader.Find("Standard"));
        }

        Color matColor = data.tintWithBlockColor ? data.blockColor : Color.white;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", matColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", matColor);
        rend.material = mat;

        if (SimulationClock.Instance != null) SimulationClock.Instance.OnSpeedChanged += HandleSpeedChange;
    }

    void Update()
    {
        if (SimulationClock.Instance == null) return;
        if (visualTransform != null)
        {
            visualTransform.Rotate(Vector3.up, 90f * SimulationClock.Instance.SimulationDeltaTime, Space.World);
            float newY = Mathf.Sin((SimulationClock.Instance.SimulationTime - spawnTime) * 3f) * 0.1f;
            visualTransform.localPosition = new Vector3(0, newY, 0);
        }

        // --- NEW PICKUP LOGIC ---
        // Prevents instant pickup by the miner, gives it a 0.5s grace period
        if (SimulationClock.Instance.SimulationTime > spawnTime + 0.5f)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, 1.5f);
            foreach (var hit in hits)
            {
                // Check if it's the Ghost Player
                GameModeController playerCtrl = hit.GetComponentInParent<GameModeController>();
                if (playerCtrl != null)
                {
                    playerCtrl.AddItem(blockData, itemCount, currentDurability);
                    Destroy(gameObject);
                    return;
                }

                // Check if it's an AI Pawn
                Pawn pawn = hit.GetComponentInParent<Pawn>();
                if (pawn != null && !pawn.IsPossessed)
                {
                    pawn.AddItem(blockData, itemCount, currentDurability);
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }

    private void HandleSpeedChange(SimulationClock.Speed newSpeed)
    {
        if (rb == null) return;
        if (newSpeed == SimulationClock.Speed.Paused) { savedVelocity = rb.linearVelocity; rb.isKinematic = true; isPhysicsPaused = true; }
        else { if (isPhysicsPaused) { rb.isKinematic = false; rb.linearVelocity = savedVelocity; isPhysicsPaused = false; } }
    }

    private void OnDestroy() { if (SimulationClock.Instance != null) SimulationClock.Instance.OnSpeedChanged -= HandleSpeedChange; }
}