using UnityEngine;

public enum PawnType
{
    Prey,
    Predator,
    Scavenger
}

public class PawnIdentity : MonoBehaviour
{
    [Header("1. Identity")]
    [Tooltip("The species/role of this pawn.")]
    public PawnType type;

    [Header("2. Physical Config")]
    [Tooltip("Vertical adjustment. \n0.5 = Pivot at feet. \n1.0 = Pivot at center.")]
    public float verticalOffset = 1.0f;

    [Header("3. AI Senses")]
    [Tooltip("Radius (in blocks) that this pawn can detect friends or enemies.")]
    public float sightRadius = 15f;

    void Start()
    {
        UpdateColor();
    }

    // Debug coloring based on type
    public void UpdateColor()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null) rend = GetComponentInChildren<Renderer>();

        if (rend != null)
        {
            switch (type)
            {
                case PawnType.Predator: rend.material.color = Color.red; break;
                case PawnType.Prey: rend.material.color = Color.green; break;
                case PawnType.Scavenger: rend.material.color = Color.blue; break;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRadius);
    }
}