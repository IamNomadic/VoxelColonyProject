using UnityEngine;

public enum PawnType
{
    Prey,
    Predator,
    Scavenger
}

public class PawnIdentity : MonoBehaviour
{
    [Header("Identity")]
    public PawnType type;

    [Header("Positioning")]
    [Tooltip("Lift the pawn up by this amount so it doesn't clip into the ground.")]
    public float verticalOffset = 1.0f; // Default 1.0 works well for Capsules

    [Header("Senses")]
    public float sightRadius = 15f;

    void Start()
    {
        UpdateColor();
    }

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