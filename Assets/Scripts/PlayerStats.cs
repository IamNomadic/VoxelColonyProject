
using System;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public int CurrentHunger = 100;
    public int MaxHunger = 100;
    public int HungerTickTimeGate = 5;
    [SerializeField] float HungerTickTime;

    public int MaxHealth = 100;
    public int CurrentHealth = 100;

    public float invincibilityTime = 0.5f;
    private float _invincibilityTime;

    public bool dead;
    public bool targetable;
    bool Starving;
    bool OutOfHunger = false;

    public static event Action OnPlayerDamaged;

    private void Start()
    {
        OutOfHunger = false;
        _invincibilityTime = invincibilityTime;
        CurrentHealth = MaxHealth;
        CurrentHunger = MaxHunger;
    }

    public void Update()
    {
        if (SimulationClock.Instance == null || dead) return;

        float simDelta = SimulationClock.Instance.SimulationDeltaTime;

        if (OutOfHunger == false || Starving == true)
        {
            HungerTickTime += simDelta;
        }

        if (HungerTickTime >= HungerTickTimeGate)
        {
            HungerTickTime = 0;
            CurrentHunger--;
            if (Starving)
            {
                TakeDamage(1);
            }
            OnPlayerDamaged?.Invoke();
        }

        if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
        if (CurrentHunger > MaxHunger) CurrentHunger = MaxHunger;

        if (CurrentHunger <= 0)
        {
            OutOfHunger = true;
            Starving = true;
        }
        else if (CurrentHunger > 0)
        {
            OutOfHunger = false;
            Starving = false;
        }

        if (_invincibilityTime > 0)
        {
            _invincibilityTime -= simDelta;
        }
    }

    public void RefreshHud() { OnPlayerDamaged?.Invoke(); }

    public void TakeDamage(int damage)
    {
        if (_invincibilityTime <= 0 && !dead)
        {
            CurrentHealth -= damage;
            OnPlayerDamaged?.Invoke();
            _invincibilityTime = invincibilityTime;

            if (CurrentHealth <= 0)
            {
                dead = true;
            }
        }
    }

    public void HealDamage(int Health)
    {
        if (dead) return;

        CurrentHealth += Health;
        OnPlayerDamaged?.Invoke();
    }

    public void GainHunger(int Hunger)
    {
        if (dead) return;

        CurrentHunger += Hunger;
        OnPlayerDamaged?.Invoke();
    }
}