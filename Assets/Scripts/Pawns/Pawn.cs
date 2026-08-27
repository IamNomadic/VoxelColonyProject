using System.Collections.Generic;
using UnityEngine;

public class Pawn : MonoBehaviour
{
    private static readonly HashSet<Pawn> activePawns = new HashSet<Pawn>();

    [Header("Configuration")]
    public PawnDataSO data;

    public VoxelWorld World { get; private set; }
    public PawnJobTracker JobTracker { get; private set; }

    // PAWN INVENTORY & STATS
    public InventorySlot[] inventory = new InventorySlot[36];
    public PlayerStats stats { get; private set; }

    // Logic State
    public Vector3Int LogicalGridPos { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsPossessed { get; private set; }

    // Visual Interpolation
    private Vector3 startVisualPos;
    private Vector3 targetVisualPos;
    private float moveProgress;
    private float moveSpeed = 6.0f;

    private Renderer[] allRenderers;
    private Collider pawnCollider;

    public static IEnumerable<Pawn> ActivePawns => activePawns;

    void OnEnable()
    {
        activePawns.Add(this);
    }

    void OnDisable()
    {
        activePawns.Remove(this);
    }

    void OnDestroy()
    {
        if (JobTracker != null) JobTracker.Dispose();
    }

    void Awake()
    {
        for (int i = 0; i < 36; i++) inventory[i] = new InventorySlot();
        stats = GetComponent<PlayerStats>();
    }

    void Start()
    {
        World = FindObjectOfType<VoxelWorld>();
        JobTracker = new PawnJobTracker(this);
        allRenderers = GetComponentsInChildren<Renderer>();
        pawnCollider = GetComponent<Collider>();

        SnapToGround();
        UpdateColor();
    }

    void Update()
    {
        if (stats != null && stats.dead)
        {
            Die();
            return;
        }

        if (IsPossessed) return;
        if (SimulationClock.Instance == null) return;

        float simDelta = SimulationClock.Instance.SimulationDeltaTime;

        // --- NEW: PAWN GRAVITY LOGIC ---
        // If the pawn isn't moving and the block directly below it is empty, make it fall!
        if (!IsMoving && World != null)
        {
            if (World.GetBlock(LogicalGridPos + Vector3Int.down) == null)
            {
                StartMovingTo(LogicalGridPos + Vector3Int.down);
            }
        }

        JobTracker.ExecuteTracker(simDelta);

        if (IsMoving)
        {
            moveProgress += moveSpeed * simDelta;
            if (moveProgress >= 1f)
            {
                moveProgress = 1f;
                IsMoving = false;
                transform.position = targetVisualPos;
            }
            else
            {
                transform.position = Vector3.Lerp(startVisualPos, targetVisualPos, moveProgress);
            }
        }
    }

    // --- INVENTORY HELPERS ---
    public bool HasItem(BlockData block)
    {
        foreach (var slot in inventory)
        {
            if (!slot.IsEmpty && slot.block == block) return true;
        }
        return false;
    }

    public void ConsumeItem(BlockData block)
    {
        foreach (var slot in inventory)
        {
            if (!slot.IsEmpty && slot.block == block)
            {
                slot.count--;
                if (slot.count <= 0) slot.Clear();
                return;
            }
        }
    }

    public void AddItem(BlockData block, int amount, int incomingDurability = 0)
    {
        if (block == null) return;

        // Try to stack
        for (int i = 35; i >= 0; i--)
        {
            if (!inventory[i].IsEmpty && inventory[i].block == block && inventory[i].count < inventory[i].MaxStack)
            {
                int space = inventory[i].MaxStack - inventory[i].count;
                int transfer = Mathf.Min(space, amount);
                inventory[i].count += transfer;
                amount -= transfer;
                if (amount <= 0) return;
            }
        }

        // Find empty slot
        for (int i = 0; i < 36; i++)
        {
            if (inventory[i].IsEmpty)
            {
                int transfer = Mathf.Min(block.maxToolUses > 0 ? 1 : 64, amount);
                inventory[i].block = block;
                inventory[i].count = transfer;
                inventory[i].durability = incomingDurability > 0 ? incomingDurability : block.maxToolUses;
                amount -= transfer;
                if (amount <= 0) return;
            }
        }

        // If inventory is full, drop it on the ground!
        if (amount > 0)
        {
            GameObject dropObj = new GameObject("Drop_" + block.blockName);
            VoxelItemDrop dropScript = dropObj.AddComponent<VoxelItemDrop>();
            dropScript.Initialize(block, transform.position + (Vector3.up * 0.5f), amount, incomingDurability);
        }
    }

    private void Die()
    {
        foreach (var slot in inventory)
        {
            if (!slot.IsEmpty)
            {
                GameObject dropObj = new GameObject("Drop_" + slot.block.blockName);
                VoxelItemDrop dropScript = dropObj.AddComponent<VoxelItemDrop>();
                dropScript.Initialize(slot.block, transform.position + (Vector3.up * 0.5f), slot.count, slot.durability);
            }
        }
        Destroy(gameObject);
    }

    public void Possess()
    {
        IsPossessed = true;
        if (IsMoving)
        {
            transform.position = targetVisualPos;
            IsMoving = false;
        }

        if (allRenderers != null) foreach (var rend in allRenderers) rend.enabled = false;
        if (pawnCollider != null) pawnCollider.enabled = false;
    }

    public void Unpossess(Vector3 dropPos)
    {
        transform.position = dropPos;
        SnapToGround();
        JobTracker.ClearJobs();

        if (allRenderers != null) foreach (var rend in allRenderers) rend.enabled = true;
        if (pawnCollider != null) pawnCollider.enabled = true;

        IsPossessed = false;
    }

    public void StartMovingTo(Vector3Int nextGridPos)
    {
        if (IsMoving) return;

        LogicalGridPos = nextGridPos;
        startVisualPos = transform.position;
        targetVisualPos = GetVisualPosition(nextGridPos);

        moveProgress = 0f;
        IsMoving = true;
    }

    public Vector3 GetVisualPosition(Vector3Int gridPos)
    {
        return new Vector3(gridPos.x + 0.5f, gridPos.y + data.verticalOffset, gridPos.z + 0.5f);
    }

    private void SnapToGround()
    {
        int startX = Mathf.FloorToInt(transform.position.x);
        int startZ = Mathf.FloorToInt(transform.position.z);
        int startY = Mathf.FloorToInt(transform.position.y);

        for (int y = startY; y >= 0; y--)
        {
            if (VoxelPathHelper.IsWalkable(World, new Vector3(startX, y, startZ), checkPawns: false))
            {
                LogicalGridPos = new Vector3Int(startX, y, startZ);
                transform.position = GetVisualPosition(LogicalGridPos);
                return;
            }
        }
    }

    private void UpdateColor()
    {
        if (allRenderers != null && allRenderers.Length > 0)
        {
            allRenderers[0].material.color = data.debugColor;
        }
    }
}