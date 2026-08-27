using UnityEngine;

[CreateAssetMenu(fileName = "NewPawnData", menuName = "Pawn/Data Profile")]
public class PawnDataSO : ScriptableObject
{
    [Header("Identity")]
    public string speciesName;
    public PawnType type; // Enum is still defined globally
    public Color debugColor = Color.white;

    [Header("Physical Settings")]
    [Tooltip("Vertical adjustment. 0.5 = Pivot at feet. 1.0 = Pivot at center.")]
    public float verticalOffset = 1.0f;

    [Tooltip("How fast this pawn falls.")]
    public float gravitySpeed = 15f;

    [Header("Senses")]
    [Tooltip("Radius (in blocks) that this pawn can detect friends or enemies.")]
    public float sightRadius = 15f;
}