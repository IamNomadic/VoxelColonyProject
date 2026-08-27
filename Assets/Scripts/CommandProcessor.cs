using UnityEngine;
using System.Linq;

public class CommandProcessor : MonoBehaviour
{
    public static CommandProcessor Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void Execute(string input, GameModeController controller)
    {
        string cmd = input.StartsWith("/") ? input.Substring(1) : input;
        string[] args = cmd.Split(' ');
        if (args.Length == 0) return;

        string action = args[0].ToLower();

        switch (action)
        {
            case "give":
                CmdGive(args, controller);
                break;
            case "heal":
                if (controller != null && controller.stats != null)
                {
                    controller.stats.HealDamage(100);
                    controller.stats.GainHunger(100);
                    Debug.Log("Player Healed.");
                }
                break;
            case "tp":
                CmdTeleport(args, controller);
                break;
            default:
                Debug.LogWarning($"Unknown command: {action}");
                break;
        }
    }

    private void CmdGive(string[] args, GameModeController controller)
    {
        if (args.Length < 2)
        {
            Debug.LogWarning("Usage: /give <blockName> [amount]");
            return;
        }

        string blockName = args[1];
        int count = (args.Length > 2 && int.TryParse(args[2], out int c)) ? c : 1;

        if (BlockManager.Instance == null)
        {
            Debug.LogError("BlockManager instance not found!");
            return;
        }

        // Search for block by internal name or UI name (removes spaces from UI name to allow typing easily e.g. "GrassBlock")
        BlockData data = BlockManager.Instance.loadedBlocks.FirstOrDefault(b =>
            b.name.Equals(blockName, System.StringComparison.OrdinalIgnoreCase) ||
            b.blockName.Replace(" ", "").Equals(blockName, System.StringComparison.OrdinalIgnoreCase));

        if (data != null)
        {
            if (controller != null)
            {
                controller.AddItem(data, count, data.maxToolUses);
                Debug.Log($"Gave {count} {data.blockName}");
            }
        }
        else
        {
            Debug.LogError($"Block '{blockName}' not found in BlockManager.");
        }
    }

    private void CmdTeleport(string[] args, GameModeController controller)
    {
        if (args.Length < 4)
        {
            Debug.LogWarning("Usage: /tp <x> <y> <z>");
            return;
        }

        if (float.TryParse(args[1], out float x) &&
            float.TryParse(args[2], out float y) &&
            float.TryParse(args[3], out float z))
        {
            if (controller != null)
            {
                // Disable CharacterController temporarily so Unity allows teleporting the object
                CharacterController cc = controller.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                controller.transform.position = new Vector3(x, y, z);

                if (cc != null) cc.enabled = true;

                Debug.Log($"Teleported to {x}, {y}, {z}");
            }
        }
    }
}