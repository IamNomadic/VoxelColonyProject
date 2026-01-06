using UnityEngine;

public enum PawnType
{
    Prey,       // Sheep, Cows (Flees from predators)
    Predator,   // Wolves, Zombies (Chases prey)
    Scavenger   // Large, slow, neutral (Ignores others unless provoked)
}

public class PawnIdentity : MonoBehaviour
{
    [Header("Identity")]
    public PawnType type;

    [Header("Senses")]
    [Tooltip("How far this pawn can see other pawns in world units.")]
    public float sightRadius = 15f;

    void Start()
    {
        UpdateColor();
    }

    // This checks your "Type" and dyes the mesh red/green/blue automatically
    public void UpdateColor()
    {
        // Try to find a renderer on this object (MeshRenderer or SkinnedMeshRenderer)
        Renderer rend = GetComponent<Renderer>();

        // If not found, try finding it in children (common for imported models)
        if (rend == null) rend = GetComponentInChildren<Renderer>();

        if (rend != null)
        {
            switch (type)
            {
                case PawnType.Predator:
                    rend.material.color = Color.red;    // Hostile
                    break;
                case PawnType.Prey:
                    rend.material.color = Color.green;  // Friendly/Food
                    break;
                case PawnType.Scavenger:
                    rend.material.color = Color.blue;   // Neutral
                    break;
            }
        }
    }

    // Gizmos help visualize the sensor range in the Editor
    private void OnDrawGizmosSelected()
    {
        // Draw the "smell" or "sight" radius in yellow
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRadius);
    }
}