using Godot;

using System;

[GlobalClass]
public partial class ManaState : Node
{
    private float _regenerationTimer;
    private float _regenerationDelayTimer;

    public int Current { get; private set; }

    public int Max { get; private set; } = 1;

    [Export]
    public int RegenerationAmount { get; set; } = 0;

    [Export]
    public float RegenerationInterval { get; set; } = 1.0f;

    [Export]
    public float RegenerationDelayAfterCast { get; set; } = 1.5f;

    public void Initialize(int maxMana)
    {
        Max = Math.Max(0, maxMana);
        Current = Max;
        _regenerationTimer = 0.0f;
        _regenerationDelayTimer = 0.0f;
    }

    public void SetMax(int maxMana)
    {
        Max = Math.Max(0, maxMana);
        Current = Math.Clamp(Current, 0, Max);
    }

    public void SetCurrent(int value)
    {
        Current = Math.Clamp(value, 0, Max);
    }

    public int Tick(double delta)
    {
        var deltaSeconds = Math.Max(0.0f, (float)delta);
        if (_regenerationDelayTimer > 0.0f)
            _regenerationDelayTimer = Math.Max(0.0f, _regenerationDelayTimer - deltaSeconds);

        if (Current >= Max)
        {
            _regenerationTimer = 0.0f;
            return 0;
        }

        var regenerationAmount = Math.Max(0, RegenerationAmount);
        if (regenerationAmount <= 0)
            return 0;

        if (_regenerationDelayTimer > 0.0f)
        {
            _regenerationTimer = 0.0f;
            return 0;
        }

        var regenerationInterval = Math.Max(0.0f, RegenerationInterval);
        if (regenerationInterval <= 0.0f)
            return Restore(regenerationAmount);

        _regenerationTimer += deltaSeconds;

        var restored = 0;
        while (_regenerationTimer >= regenerationInterval && Current < Max)
        {
            _regenerationTimer -= regenerationInterval;
            restored += Restore(regenerationAmount);
        }

        if (Current >= Max)
            _regenerationTimer = 0.0f;

        return restored;
    }

    public void ResetRegenerationDelay()
    {
        _regenerationDelayTimer = Math.Max(0.0f, RegenerationDelayAfterCast);
        _regenerationTimer = 0.0f;
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
