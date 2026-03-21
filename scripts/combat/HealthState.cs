using Godot;

using System;

[GlobalClass]
public partial class HealthState : Node
{
    public int Current { get; private set; }
    public int Max { get; private set; } = 1;
    public bool IsDead { get; private set; }

    public void Initialize(int maxHealth)
    {
        Max = Math.Max(1, maxHealth);
        Current = Max;
        IsDead = false;
    }

    public void SetMax(int maxHealth)
    {
        Max = Math.Max(1, maxHealth);
        Current = Math.Clamp(Current, 0, Max);
        if (Current > 0 && IsDead)
            IsDead = false;
    }

    public void SetCurrent(int value)
    {
        Current = Math.Clamp(value, 0, Max);
        IsDead = Current <= 0;
    }

    public int ApplyDamage(int amount)
    {
        var appliedDamage = Math.Max(1, amount);
        SetCurrent(Current - appliedDamage);
        return appliedDamage;
    }

    public int ApplyHealing(int amount)
    {
        if (amount <= 0 || IsDead)
            return 0;

        var healed = Math.Min(amount, Max - Current);
        if (healed <= 0)
            return 0;

        SetCurrent(Current + healed);
        return healed;
    }

    public void SetDead(bool isDead)
    {
        IsDead = isDead;
        if (isDead)
            Current = 0;
    }
}
