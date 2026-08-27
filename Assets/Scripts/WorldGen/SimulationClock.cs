using System;
using UnityEngine;

public class SimulationClock : MonoBehaviour
{
    public static SimulationClock Instance { get; private set; }

    public enum Speed
    {
        Paused = 0,
        Normal = 1,
        Fast = 2
    }

    [Header("Clock State")]
    [SerializeField] private Speed currentSpeed = Speed.Normal;

    /// <summary>
    /// The accumulated time while the simulation is actively running.
    /// Use this instead of Time.time for game logic.
    /// </summary>
    public float SimulationTime { get; private set; }

    /// <summary>
    /// The delta time for the current frame, scaled by the simulation speed.
    /// Use this instead of Time.deltaTime for game logic.
    /// </summary>
    public float SimulationDeltaTime { get; private set; }

    /// <summary>
    /// Event fired when the simulation speed changes. Useful for pausing audio or physics components.
    /// </summary>
    public event Action<Speed> OnSpeedChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        HandleInput();

        // Calculate the current frame's simulation delta based on the speed multiplier
        SimulationDeltaTime = Time.deltaTime * (float)currentSpeed;

        // Accumulate total simulation time
        SimulationTime += SimulationDeltaTime;
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            SetSpeed(Speed.Paused);
        }
        else if (Input.GetKeyDown(KeyCode.K))
        {
            SetSpeed(Speed.Normal);
        }
        else if (Input.GetKeyDown(KeyCode.L))
        {
            SetSpeed(Speed.Fast);
        }
    }

    public void SetSpeed(Speed newSpeed)
    {
        if (currentSpeed == newSpeed) return;

        currentSpeed = newSpeed;
        OnSpeedChanged?.Invoke(currentSpeed);

        Debug.Log($"[SimulationClock] Speed changed to: {currentSpeed}");
    }

    public Speed GetCurrentSpeed()
    {
        return currentSpeed;
    }
}