using UnityEngine;

public class Mode_Survival : IGameMode
{
    private GameModeController ctrl;

    // Survival specific limits
    private float interactReach = 6f; // Shorter reach than Builder
    private float interactCooldown = 0.2f;
    private float lastInteractTime = 0f;

    // Mining State
    private Vector3Int? miningTarget = null;
    private float miningProgress = 0f;

    public string ModeName => "Survival";

    public Mode_Survival(GameModeController c)
    {
        ctrl = c;
    }

    public void SetupMovement(PlayerMovement move)
    {
        // Tells the player controller to switch from Flying to Gravity/Walking
        move.SetFlying(false);
    }

    public void OnEnter() { ResetMining(); }
    public void OnExit() { ResetMining(); }

    private void ResetMining()
    {
        miningTarget = null;
        miningProgress = 0f;
    }

    public void OnUpdate(Ray ray)
    {
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, interactReach, ctrl.interactionMask);

        // 1. Break Block (Left Click & Hold)
        if (Input.GetMouseButton(0))
        {
            if (hitSomething)
            {
                Vector3 breakPosFloat = hit.point - (hit.normal * 0.05f);
                Vector3Int breakPos = new Vector3Int(
                    Mathf.FloorToInt(breakPosFloat.x),
                    Mathf.FloorToInt(breakPosFloat.y),
                    Mathf.FloorToInt(breakPosFloat.z)
                );

                // If player started looking at a different block, reset progress
                if (miningTarget != breakPos)
                {
                    miningTarget = breakPos;
                    miningProgress = 0f;
                }

                BlockData targetBlock = ctrl.world.GetBlock(breakPosFloat);
                if (targetBlock != null)
                {
                    // Negative durability means indestructible (e.g., Bedrock)
                    if (targetBlock.durability < 0f)
                    {
                        miningProgress = 0f;
                    }
                    else
                    {
                        miningProgress += Time.deltaTime;

                        // --- BLOCK IS BROKEN ---
                        if (miningProgress >= targetBlock.durability)
                        {
                            // 1. Spawn the dropped item (offset by +0.5 to spawn in center of the grid space)
                            Vector3 spawnPos = new Vector3(breakPos.x + 0.5f, breakPos.y + 0.5f, breakPos.z + 0.5f);
                            GameObject dropObj = new GameObject("Drop_" + targetBlock.blockName);
                            VoxelItemDrop dropScript = dropObj.AddComponent<VoxelItemDrop>();
                            dropScript.Initialize(targetBlock, spawnPos);

                            // 2. Erase the block from the world
                            ctrl.world.ModifyBlock(breakPosFloat, null);

                            ResetMining();
                            lastInteractTime = Time.time;
                        }
                    }
                }
            }
            else
            {
                ResetMining();
            }
        }
        else
        {
            ResetMining();
        }

        // 2. Place Block (Right Click)
        if (Input.GetMouseButton(1) && Time.time > lastInteractTime + interactCooldown)
        {
            if (hitSomething)
            {
                BlockData blockToPlace = ctrl.sharedHotbar[ctrl.currentSlotIndex];
                if (blockToPlace != null)
                {
                    Vector3 placePos = hit.point + (hit.normal * 0.05f);
                    ctrl.world.ModifyBlock(placePos, blockToPlace);
                    lastInteractTime = Time.time;
                }
            }
        }
    }

    public void OnGUI()
    {
        BlockData currentBlock = ctrl.sharedHotbar[ctrl.currentSlotIndex];
        string blockName = currentBlock != null ? currentBlock.blockName : "Empty Hand";

        GUI.Label(new Rect(20, Screen.height - 70, 400, 30), "<b>SURVIVAL MODE (Walking)</b>");
        GUI.Label(new Rect(20, Screen.height - 50, 400, 30), "[Hold L-Click] Mine | [R-Click] Place");
        GUI.Label(new Rect(20, Screen.height - 30, 400, 30), $"Equipped: <color=yellow>{blockName}</color>");

        // --- DRAW MINING PROGRESS BAR ---
        if (miningTarget.HasValue && miningProgress > 0f)
        {
            BlockData targetBlock = ctrl.world.GetBlock(new Vector3(miningTarget.Value.x, miningTarget.Value.y, miningTarget.Value.z));
            if (targetBlock != null && targetBlock.durability > 0f)
            {
                float pct = Mathf.Clamp01(miningProgress / targetBlock.durability);

                float barWidth = 120f;
                float barHeight = 10f;
                float xPos = (Screen.width / 2f) - (barWidth / 2f);
                float yPos = (Screen.height / 2f) + 30f; // Just below the crosshair

                // Draw Background (Dark Grey)
                GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                GUI.DrawTexture(new Rect(xPos, yPos, barWidth, barHeight), Texture2D.whiteTexture);

                // Draw Fill (Color shifts from Red to Green)
                GUI.color = Color.Lerp(Color.red, Color.green, pct);
                GUI.DrawTexture(new Rect(xPos, yPos, barWidth * pct, barHeight), Texture2D.whiteTexture);

                GUI.color = Color.white; // Reset GUI color so we don't tint other UI elements
            }
        }
    }
}