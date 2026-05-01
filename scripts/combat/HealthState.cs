using Godot;

using System;

[GlobalClass]
public partial class HealthState : Node
{
    [Signal]
    public delegate void ChangedEventHandler();

    private int _max = 1;

    public int Current { get; private set; }

    [Export]
    public int Max
    {
        get => _max;
        set => SetMax(value);
    }

    public bool IsDead { get; private set; }

    public void Initialize()
    {
        var previousCurrent = Current;
        var previousMax = Max;
        var previousIsDead = IsDead;

        _max = Math.Max(1, _max);
        Current = _max;
        IsDead = false;
        EmitChangedIfNeeded(previousCurrent, previousMax, previousIsDead);
    }

    public void SetMax(int maxHealth)
    {
        var previousCurrent = Current;
        var previousMax = Max;
        var previousIsDead = IsDead;

        _max = Math.Max(1, maxHealth);
        Current = Math.Clamp(Current, 0, _max);
        IsDead = Current <= 0;
        EmitChangedIfNeeded(previousCurrent, previousMax, previousIsDead);
    }

    public void SetCurrent(int value)
    {
        var previousCurrent = Current;
        var previousMax = Max;
        var previousIsDead = IsDead;

        Current = Math.Clamp(value, 0, Max);
        IsDead = Current <= 0;
        EmitChangedIfNeeded(previousCurrent, previousMax, previousIsDead);
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
        var previousCurrent = Current;
        var previousMax = Max;
        var previousIsDead = IsDead;

        IsDead = isDead;
        if (isDead)
            Current = 0;

        EmitChangedIfNeeded(previousCurrent, previousMax, previousIsDead);
    }

    public void RestoreToFull()
    {
        SetCurrent(Max);
    }

    private void EmitChangedIfNeeded(int previousCurrent, int previousMax, bool previousIsDead)
    {
        if (previousCurrent == Current && previousMax == Max && previousIsDead == IsDead)
            return;

        EmitSignal(SignalName.Changed);
    }
}
