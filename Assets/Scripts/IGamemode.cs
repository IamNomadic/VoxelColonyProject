using UnityEngine;

public interface IGameMode
{
    string ModeName { get; }
    void OnEnter();
    void OnExit();
    void OnUpdate(Ray ray); // Handles Clicks
    void OnGUI();           // Handles UI

    // Future-proofing: Call this to configure movement for this mode
    void SetupMovement(PlayerMovement movement);
}