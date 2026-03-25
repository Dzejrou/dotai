using Godot;

using System;

[GlobalClass]
public partial class ManaState : Node
{
    public int Current { get; private set; }
    [Export]
    public int Max { get; set; } = 1;

    public void Initialize()
    {
        Max = Math.Max(1, Max);
        Current = Max;
    }

    public void SetMax(int maxMana)
    {
        Max = Math.Max(1, maxMana);
        Current = Math.Clamp(Current, 0, Max);
    }

    public void SetCurrent(int value)
    {
        Current = Math.Clamp(value, 0, Max);
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
            return true;

        if (Current < amount)
            return false;

        Current -= amount;
        return true;
    }

    public int Restore(int amount)
    {
        if (amount <= 0)
            return 0;

        var restored = Math.Min(amount, Max - Current);
        if (restored <= 0)
            return 0;

        Current += restored;
        return restored;
    }
}
