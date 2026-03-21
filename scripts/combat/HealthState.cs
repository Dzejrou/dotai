using Godot;

using System;

[GlobalClass]
public partial class HealthState : Node
{
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; } = 1;
    public bool IsDead { get; private set; }

    public void Initialize(int maxHealth)
    {
        MaxHealth = Math.Max(1, maxHealth);
        CurrentHealth = MaxHealth;
        IsDead = false;
    }

    public void SetMaxHealth(int maxHealth)
    {
        MaxHealth = Math.Max(1, maxHealth);
        CurrentHealth = Math.Clamp(CurrentHealth, 0, MaxHealth);
        if (CurrentHealth > 0 && IsDead)
            IsDead = false;
    }

    public void SetCurrentHealth(int value)
    {
        CurrentHealth = Math.Clamp(value, 0, MaxHealth);
        IsDead = CurrentHealth <= 0;
    }

    public int ApplyDamage(int amount)
    {
        var appliedDamage = Math.Max(1, amount);
        SetCurrentHealth(CurrentHealth - appliedDamage);
        return appliedDamage;
    }

    public int ApplyHealing(int amount)
    {
        if (amount <= 0 || IsDead)
            return 0;

        var healed = Math.Min(amount, MaxHealth - CurrentHealth);
        if (healed <= 0)
            return 0;

        SetCurrentHealth(CurrentHealth + healed);
        return healed;
    }

    public void SetDead(bool isDead)
    {
        IsDead = isDead;
        if (isDead)
            CurrentHealth = 0;
    }
}
